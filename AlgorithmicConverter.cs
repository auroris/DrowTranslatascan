using System.Text;

namespace DrowTranslatascan
{
    /// <summary>
    /// Provides algorithmic (fallback) conversion between English and Drow for words
    /// not found in the dictionary.
    /// </summary>
    /// <remarks>
    /// The encoder applies a grapheme-substitution table to an English word, then
    /// inserts a single structural apostrophe at the first VC'V boundary to produce
    /// the characteristic Drow word shape.  The decoder reverses this process.
    /// These conversions are intentionally approximate — the dictionary should always
    /// be preferred where a match exists.
    /// </remarks>
    public class AlgorithmicConverter
    {
        // Encoding: English grapheme → Drow grapheme.
        // Digraphs must precede their constituent single-char entries so the
        // greedy left-to-right scan always picks the longest match first.
        private static readonly (string From, string To)[] EncodeTable =
        {
            // Digraphs
            ("th", "z"),   ("sh", "ss"),  ("ch", "x"),
            ("wh", "kh"),  ("ng", "nk"),  ("ph", "f"),
            // Vowels  (e→ae and o→au are the primary Drow vowel signals)
            ("e",  "ae"),  ("o",  "au"),  ("y",  "ii"),
            // Consonants
            ("w",  "j"),   ("j",  "jh"),  ("c",  "k"),   ("z",  "zz"),
        };

        // Decoding: Drow grapheme → English grapheme.
        // Longer/more-specific entries before their prefixes.
        private static readonly (string From, string To)[] DecodeTable =
        {
            ("zz", "z"),   ("ss", "sh"),  ("kh", "wh"),  ("nk", "ng"),  ("jh", "j"),
            ("ae", "e"),   ("au", "o"),   ("ii", "y"),
            ("z",  "th"),  ("x",  "ch"),  ("j",  "w"),
        };

        /// <summary>
        /// Converts an English word to its approximate Drow equivalent using the
        /// grapheme-substitution table and apostrophe-insertion rules.
        /// </summary>
        /// <param name="englishWord">The English word to convert. Must be non-empty.</param>
        /// <returns>The algorithmically generated Drow word, with capitalization preserved.</returns>
        public static string ConvertToDrow(string englishWord)
        {
            bool isFirstCap = char.IsUpper(englishWord[0]);
            bool isAllCap   = IsAllCaps(englishWord);

            string word = StripNonAlpha(englishWord.ToLower());
            if (word.Length == 0) return englishWord;

            string encoded = ApplyTable(word, EncodeTable);
            string result  = InsertApostrophe(encoded);
            return RestoreCapitalization(result, isFirstCap, isAllCap);
        }

        /// <summary>
        /// Converts a Drow word back to its approximate English equivalent by reversing
        /// the grapheme substitutions.
        /// </summary>
        /// <param name="drowWord">The Drow word to convert. Must be non-empty.</param>
        /// <returns>The algorithmically recovered English word, with capitalization preserved.</returns>
        public static string ConvertToCommon(string drowWord)
        {
            bool isFirstCap = char.IsUpper(drowWord[0]);
            bool isAllCap   = IsAllCaps(drowWord);

            // Strip structural apostrophes the encoder inserted, then decode.
            string word = StripApostrophesAndNormalize(drowWord);
            if (word.Length == 0) return drowWord;

            string decoded = ApplyTable(word, DecodeTable);
            return RestoreCapitalization(decoded, isFirstCap, isAllCap);
        }

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
        /// of an encoded Drow vowel cluster (<c>ae</c>, <c>au</c>, <c>ii</c>, <c>a</c>,
        /// <c>i</c>, or <c>u</c>), and sets <paramref name="len"/> to 1 or 2 accordingly.
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
        /// already-encoded Drow word, producing the VC'V boundary characteristic
        /// of authentic Drow orthography.  Words shorter than 5 characters are
        /// returned unchanged.
        /// </summary>
        private static string InsertApostrophe(string encoded)
        {
            if (encoded.Length < 5) return encoded;

            int i = 0;
            // Skip leading consonants
            while (i < encoded.Length && !IsEncodedVowel(encoded, i, out _)) i++;
            if (i >= encoded.Length) return encoded;

            // Skip first vowel group
            while (i < encoded.Length && IsEncodedVowel(encoded, i, out int vLen)) i += vLen;
            if (i >= encoded.Length) return encoded;

            // Skip consonant cluster
            while (i < encoded.Length && !IsEncodedVowel(encoded, i, out _)) i++;
            if (i >= encoded.Length || encoded.Length - i < 2) return encoded;

            return encoded.Substring(0, i) + '\'' + encoded.Substring(i);
        }

        /// <summary>
        /// Restores the original capitalization pattern of a word after conversion.
        /// If <paramref name="isAllCap"/> is <see langword="true"/>, the whole word is
        /// uppercased; if only <paramref name="isFirstCap"/> is <see langword="true"/>,
        /// the first letter is capitalized.
        /// </summary>
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

        /// <summary>
        /// Returns <see langword="true"/> if <paramref name="word"/> contains more than
        /// one letter and every letter is uppercase (e.g. <c>"HELLO"</c>).
        /// Single uppercase letters such as <c>"I"</c> are intentionally excluded.
        /// </summary>
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

        /// <summary>Removes all non-letter characters from <paramref name="word"/>.</summary>
        private static string StripNonAlpha(string word)
        {
            var sb = new StringBuilder(word.Length);
            foreach (char c in word)
                if (char.IsLetter(c)) sb.Append(c);
            return sb.ToString();
        }

        /// <summary>
        /// Lowercases <paramref name="word"/> and removes all apostrophes, preparing a
        /// Drow word for decoding through the substitution table.
        /// </summary>
        private static string StripApostrophesAndNormalize(string word)
        {
            var sb = new StringBuilder(word.Length);
            foreach (char c in word.ToLower())
                if (c != '\'') sb.Append(c);
            return sb.ToString();
        }
    }
}
