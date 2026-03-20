using System.Text;

namespace DrowTranslatascan
{
    /// <summary>
    /// Provides algorithmic (fallback) conversion between the common language and
    /// the constructed language for words not found in the dictionary.
    /// </summary>
    /// <remarks>
    /// The encoder applies a grapheme-substitution table to a common-language word, then
    /// optionally inserts a single structural apostrophe at the first VC'V boundary to
    /// produce the characteristic conlang word shape. The decoder reverses this process.
    /// These conversions are intentionally approximate — the dictionary should always
    /// be preferred where a match exists.
    ///
    /// All tables and settings are loaded from <c>Data/language.json</c> via
    /// <see cref="Program.Config"/>, so adapting this converter to a new conlang
    /// requires no C# changes.
    /// </remarks>
    public class AlgorithmicConverter
    {
        // Cached tuple arrays built from the config on first use.
        private static (string From, string To)[]? _encodeTable;
        private static (string From, string To)[]? _decodeTable;

        private static (string From, string To)[] EncodeTable
            => _encodeTable ??= Program.Config.AlgorithmicConverterConfig.GetEncodeTuples();

        private static (string From, string To)[] DecodeTable
            => _decodeTable ??= Program.Config.AlgorithmicConverterConfig.GetDecodeTuples();

        /// <summary>
        /// Converts a common-language word to its approximate conlang equivalent using the
        /// grapheme-substitution table and apostrophe-insertion rules.
        /// </summary>
        public static string ConvertToConlang(string commonWord)
        {
            bool isFirstCap = char.IsUpper(commonWord[0]);
            bool isAllCap   = IsAllCaps(commonWord);

            string word = StripNonAlpha(commonWord.ToLower());
            if (word.Length == 0) return commonWord;

            string encoded = ApplyTable(word, EncodeTable);
            string result = Program.Config.AlgorithmicConverterConfig.InsertApostrophe
                ? InsertApostrophe(encoded)
                : encoded;
            return RestoreCapitalization(result, isFirstCap, isAllCap);
        }

        /// <summary>
        /// Converts a conlang word back to its approximate common-language equivalent by
        /// reversing the grapheme substitutions.
        /// </summary>
        public static string ConvertToCommon(string conlangWord)
        {
            bool isFirstCap = char.IsUpper(conlangWord[0]);
            bool isAllCap   = IsAllCaps(conlangWord);

            string word = StripApostrophesAndNormalize(conlangWord);
            if (word.Length == 0) return conlangWord;

            string decoded = ApplyTable(word, DecodeTable);
            return RestoreCapitalization(decoded, isFirstCap, isAllCap);
        }

        // Keep old method names as wrappers for backward compatibility (used by tests).

        /// <summary>Alias for <see cref="ConvertToConlang"/> (backward compatibility).</summary>
        public static string ConvertToDrow(string englishWord) => ConvertToConlang(englishWord);

        // ── Core helpers ────────────────────────────────────────────────────────

        /// <summary>
        /// Performs a greedy left-to-right scan of <paramref name="input"/>, replacing
        /// each matching grapheme sequence found in <paramref name="table"/> with its
        /// corresponding target, and copying unmatched characters unchanged.
        /// </summary>
        private static string ApplyTable(string input, (string From, string To)[] table)
        {
            var sb = new StringBuilder(input.Length * 2);
            int i = 0;
            while (i < input.Length)
            {
                bool matched = false;
                foreach (var (from, to) in table)
                {
                    if (from.Length <= input.Length - i &&
                        input.AsSpan(i, from.Length).SequenceEqual(from.AsSpan()))
                    {
                        sb.Append(to);
                        i += from.Length;
                        matched = true;
                        break;
                    }
                }
                if (!matched) { sb.Append(input[i]); i++; }
            }
            return sb.ToString();
        }

        /// <summary>
        /// Returns <see langword="true"/> if position <paramref name="i"/> is the start
        /// of an encoded vowel cluster and sets <paramref name="len"/> to 1 or 2 accordingly.
        /// </summary>
        private static bool IsEncodedVowel(string s, int i, out int len)
        {
            if (i + 1 < s.Length)
            {
                char a = s[i], b = s[i + 1];
                if ((a == 'a' && (b == 'e' || b == 'u')) || (a == 'i' && b == 'i'))
                { len = 2; return true; }
            }
            if (s[i] == 'a' || s[i] == 'i' || s[i] == 'u') { len = 1; return true; }
            len = 0;
            return false;
        }

        /// <summary>
        /// Inserts one structural apostrophe before the second vowel group in an
        /// already-encoded word, producing the VC'V boundary characteristic of the
        /// conlang orthography.
        /// </summary>
        private static string InsertApostrophe(string encoded)
        {
            int minLen = Program.Config.AlgorithmicConverterConfig.MinApostropheLength;
            if (encoded.Length < minLen) return encoded;

            int i = 0;
            while (i < encoded.Length && !IsEncodedVowel(encoded, i, out _)) i++;
            if (i >= encoded.Length) return encoded;

            while (i < encoded.Length && IsEncodedVowel(encoded, i, out int vLen)) i += vLen;
            if (i >= encoded.Length) return encoded;

            while (i < encoded.Length && !IsEncodedVowel(encoded, i, out _)) i++;
            if (i >= encoded.Length || encoded.Length - i < 2) return encoded;

            return encoded.Substring(0, i) + '\'' + encoded.Substring(i);
        }

        private static string RestoreCapitalization(string word, bool isFirstCap, bool isAllCap)
        {
            if (isAllCap) return word.ToUpper();
            if (isFirstCap)
            {
                for (int i = 0; i < word.Length; i++)
                {
                    if (char.IsLetter(word[i]))
                        return word.Substring(0, i) + char.ToUpper(word[i]) + word.Substring(i + 1);
                }
            }
            return word;
        }

        private static bool IsAllCaps(string word)
        {
            int letterCount = 0;
            foreach (char c in word)
            {
                if (char.IsLetter(c))
                {
                    letterCount++;
                    if (!char.IsUpper(c)) return false;
                }
            }
            return letterCount > 1;
        }

        private static string StripNonAlpha(string word)
        {
            var sb = new StringBuilder(word.Length);
            foreach (char c in word)
                if (char.IsLetter(c)) sb.Append(c);
            return sb.ToString();
        }

        private static string StripApostrophesAndNormalize(string word)
        {
            var sb = new StringBuilder(word.Length);
            foreach (char c in word.ToLower())
                if (c != '\'') sb.Append(c);
            return sb.ToString();
        }
    }
}
