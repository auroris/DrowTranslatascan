using Microsoft.Data.Sqlite;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Net;
using Humanizer;
using Microsoft.Extensions.Logging;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.OpenApi.Models;

namespace DrowTranslatascan
{
    /// <summary>
    /// Azure Functions that expose the Drow ↔ Common translation engine over HTTP,
    /// as both a plain-text endpoint and a JSON endpoint.
    /// </summary>
    public class TranslateFunction
    {
        private readonly ILogger _logger;
        private const string Common = "Common";
        private const string Drow = "Drow";

        // Compiled regex patterns
        private static readonly Regex NonWhitespaceRegex = new Regex(@"\S", RegexOptions.Compiled);
        private static readonly Regex WordCharRegex = new Regex(@"\w", RegexOptions.Compiled);

        /// <summary>Matches a word token: alphanumeric/apostrophe runs with an optional trailing hyphen segment.</summary>
        private static readonly Regex WordTokenRegex = new Regex(@"\G[\w']+\-?[\w']*", RegexOptions.Compiled);

        /// <summary>Matches a non-word token: punctuation or whitespace.</summary>
        private static readonly Regex NonWordTokenRegex = new Regex(@"\G(?:\W+|\s+)", RegexOptions.Compiled);

        private static readonly Regex PossessiveSRegex = new Regex(@"'s$", RegexOptions.Compiled);
        private static readonly Regex PluralPossessiveRegex = new Regex(@"s'$", RegexOptions.Compiled);
        private static readonly Regex TrailingVowelRegex = new Regex(@"[aeiou]$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <param name="loggerFactory">Injected logger factory.</param>
        public TranslateFunction(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<TranslateFunction>();
        }

        /// <summary>
        /// Translates text between Common and Drow, returning the result as plain text.
        /// Accepts both <c>GET</c> (query string) and <c>POST</c> (form-encoded body).
        /// </summary>
        /// <param name="req">The incoming HTTP request.</param>
        /// <param name="executionContext">Azure Functions execution context.</param>
        /// <returns>
        /// HTTP 200 with translated plain text, or HTTP 400 if required parameters are
        /// missing or invalid.
        /// </returns>
        [Function("Translate")]
        [OpenApiOperation(operationId: "Translate", tags: new[] { "translate" }, Summary = "Translate text (plain text)")]
        [OpenApiParameter(name: "text", In = ParameterLocation.Query, Type = typeof(string), Required = false, Description = "Text to translate")]
        [OpenApiParameter(name: "lang", In = ParameterLocation.Query, Type = typeof(string), Required = false, Description = "Target language: 'Drow' or 'Common'")]
        [OpenApiParameter(name: "ver", In = ParameterLocation.Query, Type = typeof(string), Required = false, Description = "If present, returns version greeting")]
        [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "text/plain", bodyType: typeof(string), Description = "Translated text")]
        [OpenApiResponseWithBody(statusCode: HttpStatusCode.BadRequest, contentType: "text/plain", bodyType: typeof(string), Description = "Missing or invalid parameters")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestData req,
            FunctionContext executionContext)
        {
            _logger.LogInformation("Processing request.");
            string? text = req.Query["text"];
            string? lang = req.Query["lang"];
            string? ver = req.Query["ver"];

            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(lang))
            {
                // Read from the body if not in query
                string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                if (!string.IsNullOrEmpty(requestBody))
                {
                    // Parse form data
                    var parsedForm = System.Web.HttpUtility.ParseQueryString(requestBody);
                    if (string.IsNullOrEmpty(text))
                        text = parsedForm["text"];
                    if (string.IsNullOrEmpty(lang))
                        lang = parsedForm["lang"];
                }
            }

            HttpResponseData response;

            if (!string.IsNullOrEmpty(ver))
            {
                response = req.CreateResponse(System.Net.HttpStatusCode.OK);
                await response.WriteStringAsync("Welcome to Drow Translatascan.");
                return response;
            }

            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(lang))
            {
                response = req.CreateResponse(System.Net.HttpStatusCode.BadRequest);
                await response.WriteStringAsync("Please provide 'text' and 'lang' parameters.");
                return response;
            }

            if (lang != Drow && lang != Common)
            {
                response = req.CreateResponse(System.Net.HttpStatusCode.BadRequest);
                await response.WriteStringAsync($"Invalid language id: {lang}");
                return response;
            }

            // Perform the translation
            string result;

            try
            {
                using (SqliteConnection connection = new SqliteConnection($"Data Source={Program.DbPath};Mode=ReadOnly"))
                {
                    connection.Open();
                    result = DoTranslation(text, lang == Drow ? Drow : Common, lang == Drow ? Common : Drow, connection);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.ToString());
                response = req.CreateResponse(System.Net.HttpStatusCode.InternalServerError);
                await response.WriteStringAsync("An internal error occurred.");
                return response;
            }

            // Return the translated text as plain text
            response = req.CreateResponse(System.Net.HttpStatusCode.OK);
            await response.WriteStringAsync(result);
            return response;
        }

        /// <summary>
        /// Translates text between Common and Drow, accepting and returning JSON.
        /// </summary>
        /// <param name="req">
        /// The incoming HTTP request. Expects a JSON body conforming to <see cref="TranslateRequest"/>.
        /// </param>
        /// <param name="executionContext">Azure Functions execution context.</param>
        /// <returns>
        /// HTTP 200 with a <see cref="TranslateResponse"/> JSON body, or HTTP 400 with an
        /// <see cref="ErrorResponse"/> JSON body if the request is malformed or missing parameters.
        /// </returns>
        [Function("TranslateJson")]
        [OpenApiOperation(operationId: "TranslateJson", tags: new[] { "translate" }, Summary = "Translate text (JSON)")]
        [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(TranslateRequest), Required = true, Description = "Translation request")]
        [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(TranslateResponse), Description = "Translation result")]
        [OpenApiResponseWithBody(statusCode: HttpStatusCode.BadRequest, contentType: "application/json", bodyType: typeof(ErrorResponse), Description = "Missing or invalid parameters")]
        public async Task<HttpResponseData> RunJson(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "TranslateJson")] HttpRequestData req,
            FunctionContext executionContext)
        {
            _logger.LogInformation("Processing JSON request.");

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            TranslateRequest? request = null;
            try
            {
                request = JsonSerializer.Deserialize<TranslateRequest>(requestBody,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch
            {
                var bad = req.CreateResponse(HttpStatusCode.BadRequest);
                await bad.WriteAsJsonAsync(new ErrorResponse { Error = "Invalid JSON body." });
                return bad;
            }

            if (string.IsNullOrEmpty(request?.Text) || string.IsNullOrEmpty(request?.Lang))
            {
                var bad = req.CreateResponse(HttpStatusCode.BadRequest);
                await bad.WriteAsJsonAsync(new ErrorResponse { Error = "Please provide 'text' and 'lang' in the request body." });
                return bad;
            }

            if (request.Lang != Drow && request.Lang != Common)
            {
                var bad = req.CreateResponse(HttpStatusCode.BadRequest);
                await bad.WriteAsJsonAsync(new ErrorResponse { Error = $"Invalid language id: {request.Lang}" });
                return bad;
            }

            string result;
            try
            {
                using var connection = new SqliteConnection($"Data Source={Program.DbPath};Mode=ReadOnly");
                connection.Open();
                result = DoTranslation(request.Text, request.Lang == Drow ? Drow : Common, request.Lang == Drow ? Common : Drow, connection);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.ToString());
                var err = req.CreateResponse(HttpStatusCode.InternalServerError);
                await err.WriteAsJsonAsync(new ErrorResponse { Error = "An internal error occurred." });
                return err;
            }

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new TranslateResponse
            {
                Translation = result,
                OriginalText = request.Text,
                TargetLanguage = request.Lang
            });
            return response;
        }

        /// <summary>
        /// Translates all word tokens in <paramref name="text"/> from <paramref name="langFrom"/>
        /// to <paramref name="langTo"/>, preserving whitespace and punctuation.
        /// Multi-word dictionary entries (up to <c>MAX_COMPOUND_LENGTH</c> words) are tried
        /// before single-word lookup. Falls back to <see cref="AlgorithmicConverter"/> for
        /// words not found in the dictionary.
        /// </summary>
        /// <param name="text">The input text to translate.</param>
        /// <param name="langTo">The target language column in the dictionary (<c>"Drow"</c> or <c>"Common"</c>).</param>
        /// <param name="langFrom">The source language column (<c>"Common"</c> or <c>"Drow"</c>).</param>
        /// <param name="connection">An open SQLite connection to the dictionary database.</param>
        /// <returns>The fully translated text with original whitespace and punctuation intact.</returns>
        static string DoTranslation(string text, string langTo, string langFrom, SqliteConnection connection)
        {
            // Tokenize the text
            List<string> tokens = Tokenize(text, out List<bool> isWord);

            int numTokens = tokens.Count;
            List<string> results = new List<string>();

            // For each token, perform translation
            int i = 0;
            while (i < numTokens)
            {
                if (!isWord[i])
                {
                    // Non-word token (punctuation / whitespace) — pass through unchanged
                    results.Add(tokens[i]);
                    i++;
                    continue;
                }

                bool translated = false;

                // Try multi-word translations (up to MAX_COMPOUND_LENGTH words).
                // Scan forward to find the longest consecutive run of word tokens
                // separated only by whitespace, then try matches from longest to shortest.
                const int MAX_COMPOUND_LENGTH = 4;
                int maxMulti = 0;
                for (int multi = 0; multi < MAX_COMPOUND_LENGTH * 2; multi++)
                {
                    int multiIdx = i + multi;
                    if (multiIdx >= numTokens)
                        break;
                    if (!isWord[multiIdx])
                    {
                        // Stop at punctuation; allow whitespace to continue the scan
                        if (NonWhitespaceRegex.IsMatch(tokens[multiIdx]))
                            break;
                    }
                    else
                    {
                        maxMulti = multi;
                    }
                }

                if (maxMulti > 0)
                {
                    for (int j = maxMulti; j >= 2; j -= 2)
                    {
                        string compoundWord = string.Concat(tokens.Skip(i).Take(j + 1));
                        string word = GetTranslation(compoundWord, langTo, langFrom, connection, out string _);
                        if (!string.IsNullOrEmpty(word))
                        {
                            results.Add(word);
                            i += j;
                            translated = true;
                            break;
                        }
                    }
                }

                if (!translated)
                {
                    string token = tokens[i];
                    translated = TryWordForms(token, langTo, langFrom, results, connection);
                }

                if (!translated && langTo == Drow)
                {
                    // Algorithmic fallback: English → Drow
                    results.Add(AlgorithmicConverter.ConvertToDrow(tokens[i]));
                    translated = true;
                }

                if (!translated && langTo == Common)
                {
                    // Algorithmic fallback: Drow → English (cipher reverse)
                    results.Add(AlgorithmicConverter.ConvertToCommon(tokens[i]));
                    translated = true;
                }

                if (!translated)
                {
                    // Could not translate
                    results.Add(tokens[i]);
                }

                i++;
            }

            // Combine results
            return string.Concat(results);
        }

        /// <summary>
        /// Attempts to translate a single word token, trying progressively more relaxed
        /// forms in order: direct lookup → possessive → plural → plural-possessive →
        /// contraction expansion. Appends the translated token(s) to <paramref name="results"/>
        /// and returns <see langword="true"/> on success.
        /// </summary>
        static bool TryWordForms(string token, string langTo, string langFrom, List<string> results, SqliteConnection connection)
        {
            // First, try direct translation
            string word = GetTranslation(token, langTo, langFrom, connection, out string _);
            if (!string.IsNullOrEmpty(word))
            {
                results.Add(word);
                return true;
            }

            // Check for possessive ('s / s')
            (string posToken, _) = UnPossessivize(token, langFrom);
            if (!string.IsNullOrEmpty(posToken))
            {
                word = GetTranslation(posToken, langTo, langFrom, connection, out _);
                if (!string.IsNullOrEmpty(word))
                {
                    results.Add(Possessivize(word));
                    return true;
                }
            }

            // Check for plural
            var unPluralizedTokens = UnPluralize(token, langFrom);
            foreach (var newToken in unPluralizedTokens)
            {
                word = GetTranslation(newToken, langTo, langFrom, connection, out _);
                if (!string.IsNullOrEmpty(word))
                {
                    results.Add(Pluralize(word, langTo));
                    return true;
                }
            }

            // Check for plural-possessive
            if (!string.IsNullOrEmpty(posToken))
            {
                unPluralizedTokens = UnPluralize(posToken, langFrom);
                foreach (var newToken in unPluralizedTokens)
                {
                    word = GetTranslation(newToken, langTo, langFrom, connection, out _);
                    if (!string.IsNullOrEmpty(word))
                    {
                        results.Add(Possessivize(Pluralize(word, langTo)));
                        return true;
                    }
                }
            }

            // Try splitting a contraction (e.g. "can't" → "can" + "not")
            var splitWords = SplitContraction(token, langFrom);
            if (splitWords.Count > 0)
            {
                foreach (var splitToken in splitWords)
                {
                    if (!WordCharRegex.IsMatch(splitToken))
                    {
                        results.Add(splitToken);
                        continue;
                    }
                    word = GetTranslation(splitToken, langTo, langFrom, connection, out _);
                    if (!string.IsNullOrEmpty(word))
                    {
                        results.Add(word);
                    }
                    else if (langTo == Drow)
                    {
                        results.Add(AlgorithmicConverter.ConvertToDrow(splitToken));
                    }
                    else
                    {
                        results.Add(splitToken);
                    }
                }
                return true;
            }

            return false;
        }

        /// <summary>
        /// Splits <paramref name="text"/> into alternating word and non-word tokens using
        /// the compiled word/non-word regex pair. Whitespace-only non-word tokens are
        /// normalised to a single space.
        /// </summary>
        /// <param name="text">The input text to tokenize.</param>
        /// <param name="isWord">
        /// Output list parallel to the returned token list; <see langword="true"/> for word
        /// tokens, <see langword="false"/> for punctuation/whitespace tokens.
        /// </param>
        /// <returns>List of token strings in their original order.</returns>
        static List<string> Tokenize(string text, out List<bool> isWord)
        {
            List<string> tokens = new List<string>();
            isWord = new List<bool>();

            int index = 0;
            while (index < text.Length)
            {
                var wordMatch = WordTokenRegex.Match(text, index);
                if (wordMatch.Success)
                {
                    tokens.Add(wordMatch.Value);
                    isWord.Add(true);
                    index += wordMatch.Length;
                }
                else
                {
                    var nonWordMatch = NonWordTokenRegex.Match(text, index);
                    if (nonWordMatch.Success)
                    {
                        var nonword = nonWordMatch.Value;
                        if (!NonWhitespaceRegex.IsMatch(nonword))
                        {
                            nonword = " ";
                        }
                        tokens.Add(nonword);
                        isWord.Add(false);
                        index += nonWordMatch.Length;
                    }
                    else
                    {
                        // Tokenizer fallback: advance one character to avoid an infinite loop
                        tokens.Add(text.Substring(index, 1));
                        isWord.Add(false);
                        index++;
                    }
                }
            }
            return tokens;
        }

        /// <summary>
        /// Looks up <paramref name="word"/> in the dictionary and returns the translation,
        /// restoring the original capitalization pattern (first-cap or ALL-CAPS) of the
        /// input word onto the result.
        /// </summary>
        /// <param name="word">The word to look up (any casing; will be lowercased for the query).</param>
        /// <param name="langTo">Target language column name.</param>
        /// <param name="langFrom">Source language column name.</param>
        /// <param name="connection">An open SQLite connection.</param>
        /// <param name="notes">Output: the Notes field from the matching dictionary row, or empty string.</param>
        /// <returns>The translated word with capitalization restored, or empty string if not found.</returns>
        static string GetTranslation(string word, string langTo, string langFrom, SqliteConnection connection, out string notes)
        {
            notes = "";
            string translation = "";
            // Handle capitalization
            bool isFirstCap = char.IsUpper(word[0]);
            bool isAllCap = word.Count(char.IsLetter) > 1 && word.All(c => !char.IsLetter(c) || char.IsUpper(c));

            string wordLower = word.ToLower();

            // Lookup in database
            string query = $"SELECT {langTo}, Notes FROM drow_dictionary WHERE {langFrom} = @word";
            using (var command = new SqliteCommand(query, connection))
            {
                command.Parameters.AddWithValue("@word", wordLower);

                using (var reader = command.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        translation = reader.GetString(0);
                        notes = !reader.IsDBNull(1) ? reader.GetString(1) : "";
                    }
                }
            }

            // Restore capitalization
            if (!string.IsNullOrEmpty(translation))
            {
                if (isFirstCap)
                    translation = char.ToUpper(translation[0]) + translation.Substring(1);
                if (isAllCap)
                    translation = translation.ToUpper();
            }

            return translation;
        }

        /// <summary>
        /// If <paramref name="word"/> ends with a possessive suffix (<c>'s</c> or <c>s'</c>),
        /// returns the base form and a label string; otherwise returns empty strings.
        /// </summary>
        static (string, string) UnPossessivize(string word, string langFrom)
        {
            if (PossessiveSRegex.IsMatch(word))
            {
                return (PossessiveSRegex.Replace(word, ""), "Possessive");
            }
            else if (PluralPossessiveRegex.IsMatch(word))
            {
                return (PluralPossessiveRegex.Replace(word, "s"), "Possessive");
            }
            else
            {
                return ("", "");
            }
        }

        /// <summary>
        /// Appends the appropriate possessive suffix to a translated word:
        /// <c>'</c> if it already ends in <c>s</c>, otherwise <c>'s</c>.
        /// </summary>
        static string Possessivize(string word)
        {
            if (word.EndsWith("s", StringComparison.OrdinalIgnoreCase))
            {
                return word + "'";
            }
            else
            {
                return word + "'s";
            }
        }

        /// <summary>
        /// Returns candidate singular/base forms of <paramref name="word"/> for dictionary
        /// lookup. For Drow, strips the <c>-n</c> or <c>-en</c> plural suffix. For Common,
        /// delegates to Humanizer's singularizer.
        /// </summary>
        static List<string> UnPluralize(string word, string langFrom)
        {
            List<string> forms = new List<string>();

            if (langFrom == Drow)
            {
                if (word.EndsWith("n"))
                    forms.Add(word.Substring(0, word.Length - 1));
                if (word.EndsWith("en"))
                    forms.Add(word.Substring(0, word.Length - 2));
            }
            else
            {
                // Use Humanizer to singularize
                string singular = word.Singularize(false);
                if (singular != word)
                    forms.Add(singular);
            }

            return forms;
        }

        /// <summary>
        /// Pluralizes a translated word using the target language's rules.
        /// Drow pluralization: vowel-ending words append <c>-n</c>; consonant-ending words
        /// append <c>-en</c>. Common pluralization delegates to Humanizer.
        /// </summary>
        static string Pluralize(string word, string langTo)
        {
            if (langTo == Drow)
            {
                // Drow pluralization: vowel-ending → +n, consonant-ending → +en
                if (TrailingVowelRegex.IsMatch(word))
                {
                    return word + "n";
                }
                else
                {
                    return word + "en";
                }
            }
            else
            {
                // Use Humanizer to pluralize
                return word.Pluralize(false);
            }
        }

        /// <summary>
        /// Attempts to expand an English contraction into its constituent words (e.g.
        /// <c>"can't"</c> → <c>["can", " ", "not"]</c>). Returns an empty list if no
        /// known contraction suffix is matched or if the source language is not Common.
        /// </summary>
        static List<string> SplitContraction(string word, string langFrom)
        {
            if (langFrom == Common)
            {
                string[] suffixes = { "'d", "'ve", "n't", "'ll", "'re", "'m", "'s" };
                string[] expansions = { "would", "have", "not", "will", "are", "am", "is" };

                for (int i = 0; i < suffixes.Length; i++)
                {
                    if (word.EndsWith(suffixes[i]))
                    {
                        string baseWord = word.Substring(0, word.Length - suffixes[i].Length);
                        return new List<string> { baseWord, " ", expansions[i] };
                    }
                }
            }
            return new List<string>();
        }
    }
}
