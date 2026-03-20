# Build Your Own Conlang Translator

This guide walks you through adapting this project to translate between English and **your** constructed language. All language-specific settings live in a single config file (`Data/language.json`), so you can get a working translator without touching any C#.

## Quick Start

1. **Fork/clone** this repository
2. **Edit** `Data/language.json` with your language's name, rules, and theme
3. **Replace** `Data/drow_dictionary.csv` with your own word list
4. **Rebuild** the database: `python scripts/reset_dictionary.py`
5. **Run**: `dotnet build && func start`
6. Visit `http://localhost:7071` to see your translator

---

## 1. The Dictionary

Your dictionary is a CSV file in `Data/` with this structure:

```csv
Drow,Common,Notes,Attribution
usstan,i,null,
dos,you,null,
abbilen,friends,null,fpe
```

**Important details:**

- **Column names** — The first two columns must match your language names exactly. If your config says `"languageName": "Elvish"` and `"commonName": "English"`, rename the columns to `Elvish,English,Notes,Attribution`.
- **All lowercase** — Every word should be lowercase. The translator handles capitalization at runtime.
- **Notes** — Optional context for a word (e.g. "formal", "archaic"). Set to `null` if unused.
- **Attribution** — Optional credit for who contributed the word. Set to `null` if unused.
- **Multi-word entries** — Compound phrases like "high elf" work and will be matched before individual words.

### Rebuilding the Database

After editing the CSV, rebuild the SQLite database:

```bash
python scripts/reset_dictionary.py
```

This reads your CSV and produces the `.db` file. Make sure the table name in `language.json` (`"databaseTable"`) matches what the script creates.

If you rename the database file, update `"databaseFile"` in `language.json` to match.

---

## 2. Language Config Reference

All configuration lives in `Data/language.json`. Here is every field:

### Core Identity

| Field | Description | Example |
|-------|-------------|---------|
| `languageName` | Your conlang's name. Used in the API, UI, and database column name. | `"Elvish"` |
| `commonName` | The natural language name (usually English). | `"English"` |
| `projectTitle` | Displayed in the web UI header and API docs. | `"Elvish Translator"` |
| `subtitle` | Tagline shown beneath the title. | `"Translate between English and the tongue of the elves"` |
| `databaseFile` | SQLite database filename inside `Data/`. | `"elvish_dictionary.db"` |
| `databaseTable` | Table name inside the database. | `"elvish_dictionary"` |

### Pluralization

Controls how your conlang forms plurals. Set `"enabled": false` if your language doesn't inflect for number (or if you handle plurals entirely through the dictionary).

```json
"pluralization": {
  "enabled": true,
  "vowelSuffix": "n",
  "consonantSuffix": "en",
  "unpluralSuffixes": ["en", "n"]
}
```

| Field | Description |
|-------|-------------|
| `enabled` | `true` to apply conlang plural rules; `false` to skip them |
| `vowelSuffix` | Suffix appended to vowel-ending words to form plurals |
| `consonantSuffix` | Suffix appended to consonant-ending words to form plurals |
| `unpluralSuffixes` | Suffixes to strip when trying to find a singular form in the dictionary. **Put longer suffixes first** to avoid partial matches (e.g. `["en", "n"]` not `["n", "en"]`). |

**How it works:** When translating *to* your conlang, if the English word is plural (detected by Humanizer), the translator looks up the singular form and then appends your suffix. When translating *from* your conlang, it strips suffixes to find the base word.

English pluralization is always handled by the Humanizer library and doesn't need configuration.

### Algorithmic Converter (Cipher Fallback)

When a word isn't in the dictionary, the translator can generate an approximate translation using phonetic substitution rules. This is optional — set `"enabled": false` if you'd rather leave unknown words untranslated.

```json
"algorithmicConverter": {
  "enabled": true,
  "encodeTable": [
    ["th", "z"], ["sh", "ss"], ["ch", "x"],
    ["e", "ae"], ["o", "au"]
  ],
  "decodeTable": [
    ["ss", "sh"], ["ae", "e"], ["au", "o"],
    ["z", "th"], ["x", "ch"]
  ],
  "insertApostrophe": true,
  "minApostropheLength": 5
}
```

| Field | Description |
|-------|-------------|
| `enabled` | `true` to use algorithmic fallback; `false` to pass unknown words through unchanged |
| `encodeTable` | Array of `[from, to]` pairs. Applied left-to-right greedily when encoding English → Conlang. **Put longer sequences (digraphs) before shorter ones.** |
| `decodeTable` | Array of `[from, to]` pairs for the reverse direction (Conlang → English). Again, longer first. |
| `insertApostrophe` | Whether to insert a structural apostrophe at vowel boundaries |
| `minApostropheLength` | Minimum encoded-word length before an apostrophe is inserted |

**Designing your encode/decode tables:**

1. **Think about what makes your language sound different.** What letter combinations appear frequently? What sounds don't exist?
2. **Map English sounds to your conlang's orthography.** For example, if your language uses "kh" where English uses "c", add `["c", "kh"]`.
3. **Order matters.** The encoder scans left-to-right and picks the first matching entry. Put multi-character entries before their constituent single characters (e.g. `["th", "z"]` before any rule for `"t"` or `"h"`).
4. **The decode table should reverse the encode table** where possible. Some mappings may be lossy (e.g. if both "ph" and "f" map to "f", you can't recover which one it was).
5. **Test round-trips.** A word encoded then decoded should ideally return the original. Check with `dotnet run` in the `drowtest/` directory.

### Contractions

Controls whether English contractions (like "can't" → "can not") are expanded before translation. This only applies when translating **from** English.

```json
"contractions": {
  "enabled": true,
  "suffixes": ["'d", "'ve", "n't", "'ll", "'re", "'m", "'s"],
  "expansions": ["would", "have", "not", "will", "are", "am", "is"]
}
```

Each suffix at index `i` expands to the word at the same index in `expansions`. Set `"enabled": false` if your common language doesn't use contractions.

### UI Theme

Customize the web interface colours:

```json
"ui": {
  "primaryColor": "#9b7fc4",
  "primaryGlow": "#5a3a8a88",
  "accentColor": "#4a2880",
  "accentHover": "#5e38a0",
  "backgroundColor": "#0d0d12",
  "cardBackground": "#14141e",
  "borderColor": "#2a2040",
  "textColor": "#c8bfa8",
  "mutedTextColor": "#7a7060",
  "inputBackground": "#0a0a10",
  "inputBorder": "#2e2550"
}
```

All values are CSS colour strings. You can use hex (`#rrggbb`), hex with alpha (`#rrggbbaa`), `rgb()`, `hsl()`, or named colours.

---

## 3. Step-by-Step Example: "Sylvan" Translator

Let's create a translator for a fictional "Sylvan" language spoken by forest fae.

### 3.1 Edit `Data/language.json`

```json
{
  "languageName": "Sylvan",
  "commonName": "English",
  "projectTitle": "Sylvan Wordweaver",
  "subtitle": "Translate between English and the whispered tongue of the fae",
  "databaseFile": "sylvan_dictionary.db",
  "databaseTable": "sylvan_dictionary",

  "pluralization": {
    "enabled": true,
    "vowelSuffix": "ri",
    "consonantSuffix": "ari",
    "unpluralSuffixes": ["ari", "ri"]
  },

  "algorithmicConverter": {
    "enabled": true,
    "encodeTable": [
      ["th", "dh"], ["sh", "sy"], ["ch", "ky"],
      ["ght", "t"], ["tion", "shae"],
      ["a", "ae"], ["e", "i"], ["u", "uu"]
    ],
    "decodeTable": [
      ["dh", "th"], ["sy", "sh"], ["ky", "ch"],
      ["shae", "tion"],
      ["ae", "a"], ["uu", "u"]
    ],
    "insertApostrophe": false,
    "minApostropheLength": 5
  },

  "contractions": {
    "enabled": true,
    "suffixes": ["'d", "'ve", "n't", "'ll", "'re", "'m", "'s"],
    "expansions": ["would", "have", "not", "will", "are", "am", "is"]
  },

  "ui": {
    "primaryColor": "#4a9b5a",
    "primaryGlow": "#2a6a3a88",
    "accentColor": "#2a7040",
    "accentHover": "#3a9050",
    "backgroundColor": "#0a120d",
    "cardBackground": "#121e14",
    "borderColor": "#1a3020",
    "textColor": "#b8c8a8",
    "mutedTextColor": "#607050",
    "inputBackground": "#080f0a",
    "inputBorder": "#1e3525"
  }
}
```

### 3.2 Create `Data/sylvan_dictionary.csv`

```csv
Sylvan,English,Notes,Attribution
mira,hello,greeting,
vaeli,goodbye,farewell,
dha,the,null,
en,and,null,
lua,moon,null,
sola,sun,null,
tira,tree,null,
felwa,forest,null,
```

### 3.3 Update `scripts/reset_dictionary.py`

The reset script needs to know your CSV filename and column names. Open it and adjust the file path and table name to match your config.

### 3.4 Rebuild and Run

```bash
python scripts/reset_dictionary.py
dotnet build
func start
```

Visit `http://localhost:7071` and you'll see "Sylvan Wordweaver" with a green forest theme.

---

## 4. Growing Your Dictionary

### Manual additions

Add rows to your CSV and rebuild the database. Keep everything lowercase.

### LLM-assisted generation

The `scripts/llm_fill_gaps.py` script can use Claude to generate new words that follow your language's phonotactic patterns. To adapt it for your conlang:

1. Update the dictionary path and column names in the script
2. Modify the LLM prompt to describe your language's sound patterns instead of Drow's
3. Update `scripts/topics.md` with semantic categories relevant to your world
4. Run the corpus analysis tools in `analysis/` against your dictionary to generate phonotactic statistics

### Finding missing words

`scripts/missing_words.py` compares your dictionary against a frequency list of common English words and shows what's missing. This helps prioritize which words to add next.

---

## 5. Advanced: Custom Morphology

The config-driven approach handles the most common pattern (suffix-based pluralization). If your conlang has more complex morphology — case systems, verb conjugations, vowel harmony, infixes — you'll need to modify the C# code.

Key files to look at:

- **`Translate.cs`** — `UnPluralize()` and `Pluralize()` are where morphological transformations happen. Add new methods following the same pattern for your additional rules.
- **`AlgorithmicConverter.cs`** — If your language needs a fundamentally different fallback strategy (e.g. syllable-based rather than grapheme-based), this is where to change it.

The existing code is structured to make this straightforward: each morphological operation is a separate method, and the `TryWordForms()` method chains them together in priority order.

---

## 6. Deployment

### Azure Functions

This project is built as an Azure Function. To deploy:

1. Create an Azure Function App (consumption plan is fine for personal use)
2. Set the `AzureWebJobsDisableHomepage` application setting to `true`
3. Deploy with `func azure functionapp publish <your-app-name>`

### Other platforms

The translation engine has no Azure-specific dependencies at its core. If you'd prefer to host elsewhere, you can extract `Translate.cs`, `AlgorithmicConverter.cs`, and `LanguageConfig.cs` into a standard ASP.NET Core web app or any other .NET host.

---

## 7. API Reference

Once running, your translator exposes these endpoints:

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/api/Translate?text=hello&lang=YourConlang` | GET/POST | Plain-text translation |
| `/api/TranslateJson` | POST | JSON translation (`{"text": "hello", "lang": "YourConlang"}`) |
| `/api/Home` | GET | Web UI |
| `/api/swagger/ui` | GET | Interactive API docs |

The `lang` parameter must be either your `languageName` or your `commonName` from the config (case-sensitive).
