using System.Data.SQLite;
using System.Text.RegularExpressions;
using Humanizer;
using Microsoft.Extensions.Logging;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.Functions.Worker;

namespace DrowTranslatascan
{
    public class TranslateFunction
    {
        private readonly ILogger _logger;
        private const string Common = "Common";
        private const string Drow = "Drow";

        // Compiled regex patterns
        private static readonly Regex NonWhitespaceRegex = new Regex(@"\S", RegexOptions.Compiled);
        private static readonly Regex WordCharRegex = new Regex(@"\w", RegexOptions.Compiled);
        private static readonly Regex WordTokenRegex = new Regex(@"\G[\w']+\-?[\w']*", RegexOptions.Compiled);
        private static readonly Regex NonWordTokenRegex = new Regex(@"\G(?:\W+|\s+)", RegexOptions.Compiled);
        private static readonly Regex PossessiveSRegex = new Regex(@"'s$", RegexOptions.Compiled);
        private static readonly Regex PluralPossessiveRegex = new Regex(@"s'$", RegexOptions.Compiled);
        private static readonly Regex TrailingVowelRegex = new Regex(@"[aeiou]$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public TranslateFunction(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<TranslateFunction>();
        }

        [Function("Translate")]
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
                using (SQLiteConnection connection = new SQLiteConnection($"Data Source={Program.DbPath};Version=3;Read Only=True;"))
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

        static string DoTranslation(string text, string langTo, string langFrom, SQLiteConnection connection)
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
                    // Non-word token
                    results.Add(tokens[i]);
                    i++;
                    continue;
                }

                bool translated = false;

                // Try multi-word translations (up to MAX_COMPOUND_LENGTH)
                const int MAX_COMPOUND_LENGTH = 4;
                int maxMulti = 0;
                // Find the largest run of words (without punctuation)
                for (int multi = 0; multi < MAX_COMPOUND_LENGTH * 2; multi++)
                {
                    int multiIdx = i + multi;
                    if (multiIdx >= numTokens)
                        break;
                    if (!isWord[multiIdx])
                    {
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

        static bool TryWordForms(string token, string langTo, string langFrom, List<string> results, SQLiteConnection connection)
        {
            // First, try direct translation
            string word = GetTranslation(token, langTo, langFrom, connection, out string _);
            if (!string.IsNullOrEmpty(word))
            {
                results.Add(word);
                return true;
            }

            // Check for possessive
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

            // Try splitting contraction
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
                        // Error in tokenizer
                        tokens.Add(text.Substring(index, 1));
                        isWord.Add(false);
                        index++;
                    }
                }
            }
            return tokens;
        }

        static string GetTranslation(string word, string langTo, string langFrom, SQLiteConnection connection, out string notes)
        {
            notes = "";
            string translation = "";
            // Handle capitalization
            bool isFirstCap = char.IsUpper(word[0]);
            bool isAllCap = word.All(c => !char.IsLetter(c) || char.IsUpper(c));

            string wordLower = word.ToLower();

            // Lookup in database
            string query = $"SELECT {langTo}, Notes FROM drow_dictionary WHERE {langFrom} = @word";
            using (var command = new SQLiteCommand(query, connection))
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
