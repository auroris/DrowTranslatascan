using System.Text.Json;
using System.Text.Json.Serialization;

namespace DrowTranslatascan
{
    /// <summary>
    /// Configuration model for a constructed language translator, loaded from
    /// <c>Data/language.json</c>. All language-specific settings live here so that
    /// adapting the project to a new conlang requires editing only this file and
    /// providing a dictionary CSV — no C# changes needed.
    /// </summary>
    public class LanguageConfig
    {
        /// <summary>Name of the constructed language (e.g. "Drow", "Elvish", "Klingon").</summary>
        [JsonPropertyName("languageName")]
        public string LanguageName { get; set; } = "Drow";

        /// <summary>Name of the natural/common language (e.g. "Common", "English").</summary>
        [JsonPropertyName("commonName")]
        public string CommonName { get; set; } = "Common";

        /// <summary>Title shown in the web UI header and OpenAPI docs.</summary>
        [JsonPropertyName("projectTitle")]
        public string ProjectTitle { get; set; } = "Drow Translatascan";

        /// <summary>Subtitle/tagline shown beneath the header.</summary>
        [JsonPropertyName("subtitle")]
        public string Subtitle { get; set; } = "Translate between Common and the tongue of the dark elves";

        /// <summary>Filename of the SQLite database inside the <c>Data/</c> folder.</summary>
        [JsonPropertyName("databaseFile")]
        public string DatabaseFile { get; set; } = "drow_dictionary.db";

        /// <summary>Name of the table inside the SQLite database.</summary>
        [JsonPropertyName("databaseTable")]
        public string DatabaseTable { get; set; } = "drow_dictionary";

        /// <summary>Pluralization rules for the constructed language.</summary>
        [JsonPropertyName("pluralization")]
        public PluralizationConfig Pluralization { get; set; } = new();

        /// <summary>Settings for the algorithmic (cipher) fallback converter.</summary>
        [JsonPropertyName("algorithmicConverter")]
        public AlgorithmicConverterConfig AlgorithmicConverterConfig { get; set; } = new();

        /// <summary>English contraction expansion settings.</summary>
        [JsonPropertyName("contractions")]
        public ContractionsConfig Contractions { get; set; } = new();

        /// <summary>Web UI theme colours.</summary>
        [JsonPropertyName("ui")]
        public UiConfig Ui { get; set; } = new();

        /// <summary>
        /// Loads a <see cref="LanguageConfig"/> from the given JSON file path.
        /// Returns the default Drow configuration if the file does not exist.
        /// </summary>
        public static LanguageConfig Load(string path)
        {
            if (!File.Exists(path))
                return new LanguageConfig();

            string json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<LanguageConfig>(json) ?? new LanguageConfig();
        }
    }

    public class PluralizationConfig
    {
        /// <summary>Whether conlang-specific pluralization is enabled.</summary>
        [JsonPropertyName("enabled")]
        public bool Enabled { get; set; } = true;

        /// <summary>Suffix appended to vowel-ending words to form plurals (e.g. "n").</summary>
        [JsonPropertyName("vowelSuffix")]
        public string VowelSuffix { get; set; } = "n";

        /// <summary>Suffix appended to consonant-ending words to form plurals (e.g. "en").</summary>
        [JsonPropertyName("consonantSuffix")]
        public string ConsonantSuffix { get; set; } = "en";

        /// <summary>
        /// Suffixes to strip when attempting to un-pluralize a conlang word for dictionary lookup.
        /// Longest suffixes should come first to avoid partial matches.
        /// </summary>
        [JsonPropertyName("unpluralSuffixes")]
        public string[] UnpluralSuffixes { get; set; } = { "en", "n" };
    }

    public class AlgorithmicConverterConfig
    {
        /// <summary>Whether the algorithmic fallback converter is active.</summary>
        [JsonPropertyName("enabled")]
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Grapheme substitution table for encoding Common → Conlang.
        /// Each entry is [from, to]. Longer entries (digraphs) should precede
        /// shorter ones so the greedy scanner picks the longest match.
        /// </summary>
        [JsonPropertyName("encodeTable")]
        public string[][] EncodeTable { get; set; } = {
            new[]{"th","z"}, new[]{"sh","ss"}, new[]{"ch","x"},
            new[]{"wh","kh"}, new[]{"ng","nk"}, new[]{"ph","f"},
            new[]{"e","ae"}, new[]{"o","au"}, new[]{"y","ii"},
            new[]{"w","j"}, new[]{"j","jh"}, new[]{"c","k"}, new[]{"z","zz"}
        };

        /// <summary>
        /// Grapheme substitution table for decoding Conlang → Common.
        /// Each entry is [from, to]. Longer entries should precede shorter ones.
        /// </summary>
        [JsonPropertyName("decodeTable")]
        public string[][] DecodeTable { get; set; } = {
            new[]{"zz","z"}, new[]{"ss","sh"}, new[]{"kh","wh"}, new[]{"nk","ng"}, new[]{"jh","j"},
            new[]{"ae","e"}, new[]{"au","o"}, new[]{"ii","y"},
            new[]{"z","th"}, new[]{"x","ch"}, new[]{"j","w"}
        };

        /// <summary>Whether to insert a structural apostrophe in encoded words.</summary>
        [JsonPropertyName("insertApostrophe")]
        public bool InsertApostrophe { get; set; } = true;

        /// <summary>Minimum encoded-word length before an apostrophe is inserted.</summary>
        [JsonPropertyName("minApostropheLength")]
        public int MinApostropheLength { get; set; } = 5;

        /// <summary>Converts the encode table arrays to the tuple format used by the converter engine.</summary>
        public (string From, string To)[] GetEncodeTuples()
            => EncodeTable.Select(e => (e[0], e[1])).ToArray();

        /// <summary>Converts the decode table arrays to the tuple format used by the converter engine.</summary>
        public (string From, string To)[] GetDecodeTuples()
            => DecodeTable.Select(e => (e[0], e[1])).ToArray();
    }

    public class ContractionsConfig
    {
        /// <summary>Whether contraction expansion is enabled for the common language.</summary>
        [JsonPropertyName("enabled")]
        public bool Enabled { get; set; } = true;

        /// <summary>Contraction suffixes to detect (e.g. "'d", "n't").</summary>
        [JsonPropertyName("suffixes")]
        public string[] Suffixes { get; set; } = { "'d", "'ve", "n't", "'ll", "'re", "'m", "'s" };

        /// <summary>Full-word expansions corresponding to each suffix.</summary>
        [JsonPropertyName("expansions")]
        public string[] Expansions { get; set; } = { "would", "have", "not", "will", "are", "am", "is" };
    }

    public class UiConfig
    {
        [JsonPropertyName("primaryColor")]
        public string PrimaryColor { get; set; } = "#9b7fc4";

        [JsonPropertyName("primaryGlow")]
        public string PrimaryGlow { get; set; } = "#5a3a8a88";

        [JsonPropertyName("accentColor")]
        public string AccentColor { get; set; } = "#4a2880";

        [JsonPropertyName("accentHover")]
        public string AccentHover { get; set; } = "#5e38a0";

        [JsonPropertyName("backgroundColor")]
        public string BackgroundColor { get; set; } = "#0d0d12";

        [JsonPropertyName("cardBackground")]
        public string CardBackground { get; set; } = "#14141e";

        [JsonPropertyName("borderColor")]
        public string BorderColor { get; set; } = "#2a2040";

        [JsonPropertyName("textColor")]
        public string TextColor { get; set; } = "#c8bfa8";

        [JsonPropertyName("mutedTextColor")]
        public string MutedTextColor { get; set; } = "#7a7060";

        [JsonPropertyName("inputBackground")]
        public string InputBackground { get; set; } = "#0a0a10";

        [JsonPropertyName("inputBorder")]
        public string InputBorder { get; set; } = "#2e2550";
    }
}
