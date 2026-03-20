# LLM Guide: Helping a User Build a Conlang Translator

You are assisting someone who wants to create a translator for their constructed language (conlang) using this project as a template. This file tells you what to ask, what files to read, what to edit, and how the system fits together.

## Phase 1: Understand What the User Already Has

Before touching any files, find out where the user is starting from. Ask these questions (skip any the user has already answered):

### Essential questions

1. **What is your language called?** (e.g. "Sylvan", "Thalassian", "Orcish")
2. **What is the common/natural language?** Usually English, but could be another language.
3. **Do you have an existing word list?** If yes, what format is it in? (spreadsheet, text file, wiki page, just in their head, etc.)
4. **How large is your vocabulary?** Rough count — 50 words, 500, 5000?

### Phonology and aesthetics

5. **What should your language sound like?** Ask for adjectives (harsh, flowing, guttural, musical) or real-world language inspirations (e.g. "like Welsh", "like Japanese", "like Arabic").
6. **Are there sounds or letter combinations that are distinctive in your language?** For example: doubled consonants, apostrophes, specific digraphs like "th" or "kh", unusual vowel sequences.
7. **Are there sounds or letters your language avoids?** For example: no "f", no "w", no consonant clusters.
8. **What alphabet/script does your language use?** Almost always Latin for this project, but worth confirming.

### Grammar (only what the translator needs)

9. **How does your language form plurals?** Suffix (like English "-s")? Prefix? Vowel change? No plural marking at all?
10. **Does your language use possessives?** If so, how are they formed?
11. **Does the common language use contractions?** (English does: "can't", "won't". Other languages may not.)

### Project identity

12. **What should the project be called?** (e.g. "Sylvan Wordweaver", "Orcish Translator")
13. **What tagline/subtitle do you want?** (e.g. "Translate between English and the whispered tongue of the fae")
14. **What colour theme suits your language?** Dark purples for shadow languages, greens for nature, reds for infernal, etc. Or ask the user to name two or three colours and you can pick appropriate hex values.

## Phase 2: Read the Project Files

Before making any changes, read these files to understand the system:

| File | Why |
|------|-----|
| `TUTORIAL.md` | The user-facing tutorial. Covers everything at a high level. Read this first. |
| `Data/language.json` | The config file you will edit. Contains all language-specific settings. |
| `LanguageConfig.cs` | The C# model that deserializes the config. Shows you what fields exist and their defaults. |
| `Data/drow_dictionary.csv` | Example dictionary format. Your user's dictionary must follow this structure. |
| `analysis/corpus_analysis.txt` | Shows what kind of phonotactic analysis is possible. Useful as a template for the user's language. |
| `scripts/llm_fill_gaps.py` | The LLM-assisted dictionary expansion script. You may need to adapt its prompts for the new language. |
| `scripts/reset_dictionary.py` | Rebuilds the database from CSV. Column names and table name must match the config. |

## Phase 3: Build the Config

Edit `Data/language.json` based on the user's answers. Here is what each section needs:

### Core identity

```json
{
  "languageName": "...",       // Answer to Q1
  "commonName": "...",         // Answer to Q2, usually "English" or "Common"
  "projectTitle": "...",       // Answer to Q12
  "subtitle": "...",           // Answer to Q13
  "databaseFile": "..._dictionary.db",  // Use the language name, lowercase
  "databaseTable": "..._dictionary"     // Same, with underscores
}
```

### Pluralization

Based on answers to Q9. Examples:

**Suffix-based (like the Drow default):**
```json
"pluralization": {
  "enabled": true,
  "vowelSuffix": "ri",        // appended to vowel-ending words
  "consonantSuffix": "ari",   // appended to consonant-ending words
  "unpluralSuffixes": ["ari", "ri"]  // longest first!
}
```

**No plural marking:**
```json
"pluralization": {
  "enabled": false,
  "vowelSuffix": "", "consonantSuffix": "", "unpluralSuffixes": []
}
```

**Single universal suffix (e.g. always add "-en"):**
```json
"pluralization": {
  "enabled": true,
  "vowelSuffix": "en",
  "consonantSuffix": "en",
  "unpluralSuffixes": ["en"]
}
```

### Algorithmic converter (cipher fallback)

This is the hardest part to design. The converter transforms unknown English words into plausible conlang words using a substitution table. Based on answers to Q5-Q7:

**Design principles:**
- Map English sounds that don't exist in the conlang to sounds that do
- Map common English patterns to distinctive conlang patterns
- Put multi-character mappings (digraphs) BEFORE single characters in the table
- The decode table should reverse the encode table where possible
- Some mappings will be lossy (two English sounds mapping to one conlang sound) — that's fine

**Example: a harsh, guttural language:**
```json
"algorithmicConverter": {
  "enabled": true,
  "encodeTable": [
    ["th", "k"],  ["sh", "zh"],  ["ch", "kh"],
    ["ph", "f"],  ["wh", "gr"],
    ["e", "u"],   ["a", "o"],
    ["w", "v"],   ["j", "zh"],   ["c", "k"]
  ],
  "decodeTable": [
    ["zh", "sh"],  ["kh", "ch"],  ["gr", "wh"],
    ["u", "e"],    ["o", "a"],
    ["v", "w"],    ["k", "th"]
  ],
  "insertApostrophe": false,
  "minApostropheLength": 5
}
```

**Example: a flowing, musical language:**
```json
"algorithmicConverter": {
  "enabled": true,
  "encodeTable": [
    ["th", "l"],   ["sh", "si"],  ["ch", "ti"],
    ["ck", "k"],   ["ng", "nn"],
    ["a", "ai"],   ["u", "uu"],
    ["w", "v"],    ["j", "y"],    ["x", "ss"]
  ],
  "decodeTable": [
    ["si", "sh"],  ["ti", "ch"],  ["nn", "ng"],
    ["ai", "a"],   ["uu", "u"],
    ["v", "w"],    ["y", "j"],    ["l", "th"]
  ],
  "insertApostrophe": false,
  "minApostropheLength": 5
}
```

**If the user doesn't want algorithmic conversion** (only dictionary words translate):
```json
"algorithmicConverter": {
  "enabled": false,
  "encodeTable": [], "decodeTable": [],
  "insertApostrophe": false, "minApostropheLength": 5
}
```

**Tips for designing encode/decode tables:**
- Look at what letters are *over-represented* in the user's existing vocabulary vs English. Map English-heavy letters to conlang-heavy ones.
- Vowel mappings have the biggest impact on "feel". Mapping `e` to `ae` feels elvish; mapping `a` to `o` feels orcish.
- Apostrophe insertion at vowel boundaries gives a Tolkien-elf / Star Wars feel. Skip it for languages that should feel more grounded.
- Test with a few common words: does "dragon", "sword", "friend", "mountain" look right when converted? Iterate on the table until it does.

### Contractions

Based on Q11. If the common language is English, the defaults are fine:

```json
"contractions": {
  "enabled": true,
  "suffixes": ["'d", "'ve", "n't", "'ll", "'re", "'m", "'s"],
  "expansions": ["would", "have", "not", "will", "are", "am", "is"]
}
```

If the common language doesn't use contractions, set `"enabled": false`.

### UI theme

Based on Q14. Pick hex colours that match the language's vibe. Here are some starting palettes:

**Dark/shadow (default Drow):** purples and golds on near-black
**Forest/nature:** greens and warm browns on dark green
**Infernal/fire:** deep reds and oranges on very dark red
**Ocean/water:** teals and silvers on dark blue
**Ice/frost:** pale blues and whites on dark slate

You should generate specific hex values. The fields are:

```json
"ui": {
  "primaryColor": "...",      // Header text, link hover colour
  "primaryGlow": "...",       // Text shadow behind the header (use alpha, e.g. #rrggbbaa)
  "accentColor": "...",       // Translate button background
  "accentHover": "...",       // Translate button hover (slightly lighter)
  "backgroundColor": "...",   // Page background (very dark)
  "cardBackground": "...",    // Card/panel background (slightly lighter than page)
  "borderColor": "...",       // Card border colour
  "textColor": "...",         // Main body text
  "mutedTextColor": "...",    // Subtitle and secondary text
  "inputBackground": "...",   // Textarea background
  "inputBorder": "..."        // Textarea border
}
```

## Phase 4: Build the Dictionary

### If the user has an existing word list

Help them convert it to the required CSV format:

```
ConlangName,CommonName,Notes,Attribution
word1,translation1,null,
word2,translation2,context note,contributor
```

- First row is the header. Column names must match `languageName` and `commonName` from the config.
- All words lowercase.
- Notes and Attribution can be `null` if unused.
- No quotes needed unless a field contains commas.

### If the user is starting from scratch

Help them build a starter vocabulary. A good minimal dictionary covers:

1. **Pronouns** (I, you, he, she, it, we, they) — ~10 words
2. **Common verbs** (be, have, go, come, see, know, want, give, take, make) — ~20 words
3. **Basic nouns** (person, man, woman, child, friend, enemy, water, fire, earth, sky) — ~30 words
4. **Adjectives** (good, bad, big, small, old, new, dark, light, strong, weak) — ~20 words
5. **Function words** (the, a, and, or, not, in, on, at, to, from, with, of) — ~20 words
6. **Numbers** (one through ten, hundred, thousand) — ~15 words
7. **Greetings and common phrases** (hello, goodbye, yes, no, please, thank you) — ~10 words

That gives ~125 words, enough for the translator to be usable. When inventing words:

- **Stay consistent with phonotactics.** If the language uses "th" heavily, use it in the core vocabulary too.
- **Create recognizable roots.** If "fire" is "nar", then "fiery" could be "narith", "fireball" could be "nar'keth", etc. This makes the language feel systematic.
- **Match word length to mood.** Short, punchy words (3-4 letters) for common concepts. Longer words (6-8 letters) for abstract or formal concepts.
- **Avoid English cognates.** "Magikal" for magic is boring. Invent something fresh.

### Rebuilding the database

After the CSV is ready, the user needs to rebuild the SQLite database. The `scripts/reset_dictionary.py` script handles this, but its column names and table name are currently hardcoded for Drow. Either:

1. Edit the script to match the new column/table names, or
2. Just use a quick Python one-liner:

```python
import csv, sqlite3
from pathlib import Path
db = Path("Data/your_dictionary.db")
db.unlink(missing_ok=True)
conn = sqlite3.connect(db)
conn.execute("CREATE TABLE your_dictionary (YourLang TEXT, English TEXT, Notes TEXT)")
with open("Data/your_dictionary.csv", newline="", encoding="utf-8") as f:
    reader = csv.reader(f)
    next(reader)  # skip header
    conn.executemany("INSERT INTO your_dictionary VALUES (?,?,?)",
                     [(r[0], r[1], r[2] if len(r) > 2 else None) for r in reader])
conn.commit()
conn.close()
```

Replace `your_dictionary`, `YourLang`, and `English` with the actual names from the config.

## Phase 5: Expand the Dictionary with LLM Assistance

The `scripts/llm_fill_gaps.py` script uses Claude CLI to generate new words. To adapt it for a new conlang:

### What to change in the script

1. **`_PREAMBLE`** — Replace the Drow-specific description with one for the new language. Include:
   - The language's name and cultural context
   - What it should sound like
   - Any existing lore or inspirations
   - Instructions to skip modern/anachronistic words if it's a fantasy language

2. **`_shared_context` guidelines section** — Replace the Drow phonotactic guidelines with ones for the new language. Use the analysis tools to generate these:
   ```bash
   cd analysis
   python analyse_corpus.py    # generates corpus_analysis.txt
   python phoneme_pairs.py     # generates phoneme_pairs.txt
   ```
   These scripts read the dictionary and produce letter frequency, bigram, trigram, and vowel-sequence statistics. Use the output to write guidelines like:
   - Which letters are over/under-represented vs English
   - Common bigrams and trigrams
   - Typical word beginnings and endings
   - Vowel/consonant balance
   - Apostrophe density (if any)
   - Average word length

3. **File paths** — Update `CSV_PATH`, `DB_PATH` to point to the new dictionary files.

4. **Column names** — The `load_db()` function uses `SELECT Drow, Common FROM drow_dictionary`. Update these to match the new column and table names.

5. **`parse_llm_csv()`** — Update the expected CSV column names (`english`, `drow`) to match the new language pair.

6. **`scripts/topics.md`** — Rewrite the topic list to match the culture and setting of the new language. A forest-fae language needs plant, weather, and craft vocabulary. A martial orc language needs weapon, rank, and territory vocabulary.

### Running the expansion

```bash
# Generate words from the frequency list
python scripts/llm_fill_gaps.py --want 50

# Generate words for a specific topic
python scripts/llm_fill_gaps.py --topic "weather and seasons"

# Preview without modifying files
python scripts/llm_fill_gaps.py --topic "kinship terms" --dry-run
```

## Phase 6: Verify Everything Works

After making all changes:

```bash
# Build
dotnet build

# Run the tests (they test the algorithmic converter with whatever config is loaded)
cd drowtest && dotnet run && cd ..

# Start the server
func start
```

Then check:
- Web UI at `http://localhost:7071` shows the correct title, subtitle, and colours
- Translating a known dictionary word works in both directions
- Translating an unknown word produces a plausible conlang word (if algorithmic converter is enabled)
- The API docs at `/api/swagger/ui` show the correct language names

## Quick Reference: File Locations

| What | Where |
|------|-------|
| Language config | `Data/language.json` |
| Dictionary CSV | `Data/*.csv` |
| Dictionary DB | `Data/*.db` |
| Config C# model | `LanguageConfig.cs` |
| Translation engine | `Translate.cs` |
| Algorithmic converter | `AlgorithmicConverter.cs` |
| Web UI | `HomeFunction.cs` |
| Startup/config loading | `Program.cs` |
| DB rebuild script | `scripts/reset_dictionary.py` |
| LLM gap-filler | `scripts/llm_fill_gaps.py` |
| Topic suggestions | `scripts/topics.md` |
| Corpus analysis tools | `analysis/analyse_corpus.py`, `analysis/phoneme_pairs.py` |
| Corpus analysis output | `analysis/corpus_analysis.txt`, `analysis/phoneme_pairs.txt` |
| Tutorial for humans | `TUTORIAL.md` |
| Tests | `drowtest/Program.cs` |
