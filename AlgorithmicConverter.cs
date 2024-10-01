using Humanizer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace DrowTranslatascan
{
    public class AlgorithmicConverter {
        // Drow phoneme mappings
        private static readonly Dictionary<string, string> PhonemeMappings = new Dictionary<string, string>
        {
            // Vowels
            { "a", "ae" },
            { "e", "ei" },
            { "i", "ii" },
            { "o", "ou" },
            { "u", "uu" },
            { "æ", "ae" },
            { "ɛ", "ei" },
            { "ɪ", "i" },
            { "ɔ", "o" },
            { "ʊ", "u" },
            { "ə", "a" },
            // Consonants
            { "b", "b" },
            { "d", "d" },
            { "f", "f" },
            { "g", "g" },
            { "h", "h" },
            { "j", "y" },
            { "k", "k" },
            { "l", "l" },
            { "m", "m" },
            { "n", "n" },
            { "p", "p" },
            { "r", "r" },
            { "s", "s" },
            { "t", "t" },
            { "v", "v" },
            { "w", "w" },
            { "z", "z" },
            // Special consonant clusters
            { "ʃ", "ss" },  // sh
            { "ʒ", "zh" },  // zh
            { "θ", "th" },  // th (as in 'thin')
            { "ð", "dh" },  // th (as in 'this')
            { "ŋ", "ng" },
            { "tʃ", "ch" },
            { "dʒ", "j" },
            { "sh", "ss" },
            { "ch", "x" },
            { "th", "z" },
            { "ph", "f" }
        };

        // Drow prefixes, infixes, and suffixes
        private static readonly List<string> DrowPrefixes = new List<string>
        {
            "bel'", "elg'", "il'", "kil'", "lil'", "myr'", "quar'", "ssin'", "ul'", "z'"
        };

        private static readonly List<string> DrowSuffixes = new List<string>
        {
            "'ra", "'riia", "'rin", "'tyrr", "'vayas", "'zair", "'vyl", "'xun", "'yrr", "'zyr"
        };

        private static readonly List<string> DrowInfixes = new List<string>
        {
            "d'", "dr'", "l'", "n'", "r'", "s'", "v'", "x'", "z'"
        };

        public static string ConvertToDrow(string englishWord)
        {
            // Step 1: Normalize the word
            string word = englishWord.ToLower();
            word = Regex.Replace(word, "[^a-z]", "");

            // Step 2: Get phonetic transcription
            string phonetic = GetPhoneticTranscription(word);

            // Step 3: Map English phonemes to Drow phonemes
            string drowPhonetic = MapPhonemes(phonetic);

            // Step 4: Break into syllables using Humanizer
            List<string> syllables = BreakIntoSyllables(word);

            // Step 5: Transform syllables
            List<string> drowSyllables = TransformSyllables(syllables, drowPhonetic);

            // Step 6: Add Drow linguistic elements
            string drowWord = AssembleDrowWord(drowSyllables, word);

            // Step 7: Capitalize the first letter
            drowWord = char.ToUpper(drowWord[0]) + drowWord.Substring(1);

            return drowWord;
        }

        private static string GetPhoneticTranscription(string word)
        {
            // For simplicity, use a basic mapping (a real implementation would use IPA)
            // Here we use the word itself as a placeholder
            return word;
        }


        private static List<string> BreakIntoSyllables(string word)
        {
            List<string> syllables = new List<string>();
            StringBuilder syllableBuilder = new StringBuilder();
            bool lastCharWasVowel = false;

            for (int i = 0; i < word.Length; i++)
            {
                char currentChar = word[i];
                bool isVowel = "aeiou".Contains(currentChar);

                syllableBuilder.Append(currentChar);

                // If we encounter a vowel followed by a consonant, this might indicate a syllable boundary
                if (i > 0 && lastCharWasVowel && !isVowel)
                {
                    // Check next character to avoid splitting too early
                    if (i + 1 < word.Length && !"aeiou".Contains(word[i + 1]))
                    {
                        syllables.Add(syllableBuilder.ToString());
                        syllableBuilder.Clear();
                    }
                }

                lastCharWasVowel = isVowel;
            }

            // Add the last syllable
            if (syllableBuilder.Length > 0)
            {
                syllables.Add(syllableBuilder.ToString());
            }

            return syllables;
        }

        private static string MapPhonemes(string phonetic)
        {
            StringBuilder sb = new StringBuilder();

            for (int i = 0; i < phonetic.Length; i++)
            {
                string phoneme = phonetic[i].ToString();

                // Check for digraphs (e.g., 'th', 'sh', 'ch')
                if (i + 1 < phonetic.Length)
                {
                    string digraph = phonetic.Substring(i, 2);
                    if (PhonemeMappings.ContainsKey(digraph))
                    {
                        sb.Append(PhonemeMappings[digraph]);
                        i++; // Skip next character
                        continue;
                    }
                }

                if (PhonemeMappings.ContainsKey(phoneme))
                {
                    sb.Append(PhonemeMappings[phoneme]);
                }
                else
                {
                    sb.Append(phoneme);
                }
            }

            return sb.ToString();
        }

        private static List<string> TransformSyllables(IEnumerable<string> syllables, string drowPhonetic)
        {
            List<string> drowSyllables = new List<string>();
            Random rnd = new Random(ComputeSeed(drowPhonetic));

            foreach (var syllable in syllables)
            {
                string transformedSyllable = MapPhonemes(syllable);

                // Insert infix randomly
                if (transformedSyllable.Length > 2 && rnd.NextDouble() < 0.5)
                {
                    string infix = DrowInfixes[rnd.Next(DrowInfixes.Count)];
                    int insertPos = rnd.Next(1, transformedSyllable.Length - 1);
                    transformedSyllable = transformedSyllable.Insert(insertPos, infix);
                }

                // Possibly modify vowels
                transformedSyllable = ModifyVowels(transformedSyllable, rnd);

                drowSyllables.Add(transformedSyllable);
            }

            return drowSyllables;
        }

        private static string ModifyVowels(string syllable, Random rnd)
        {
            // Elongate vowels or add diphthongs
            syllable = Regex.Replace(syllable, "[aeiou]", m =>
            {
                if (rnd.NextDouble() < 0.5)
                {
                    return m.Value + m.Value; // Double the vowel
                }
                else
                {
                    return m.Value;
                }
            });

            return syllable;
        }

        private static string AssembleDrowWord(List<string> syllables, string originalWord)
        {
            Random rnd = new Random(ComputeSeed(originalWord));

            // Possibly add a prefix and/or suffix
            if (rnd.NextDouble() < 0.7)
            {
                string prefix = DrowPrefixes[rnd.Next(DrowPrefixes.Count)];
                syllables.Insert(0, prefix);
            }

            if (rnd.NextDouble() < 0.7)
            {
                string suffix = DrowSuffixes[rnd.Next(DrowSuffixes.Count)];
                syllables.Add(suffix);
            }

            // Assemble the word
            string drowWord = string.Join("", syllables);

            // Introduce apostrophes at consonant clusters
            drowWord = Regex.Replace(drowWord, "([b-df-hj-np-tv-z]{2,})", "'$1");

            return drowWord;
        }

        private static int ComputeSeed(string word)
        {
            // Compute a consistent seed based on the word
            unchecked
            {
                int seed = 17;
                foreach (char c in word)
                {
                    seed = seed * 31 + c;
                }
                return seed;
            }
        }
    }
}
