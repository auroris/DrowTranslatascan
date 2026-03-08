# Dictionary Gap-Filling Workflow

Two ways to expand the Drow dictionary with translations for common English words
that are currently missing.

- **Automated (recommended):** `llm_fill_gaps.py` — calls Claude, generates words,
  patches files in one step.
- **Manual:** run `missing_words.py`, hand-craft words, edit `patch_missing_words.py`.

---

## Automated workflow — `llm_fill_gaps.py`

### Prerequisites

The `claude` CLI must be on your PATH and authenticated.  If you are already
running Claude Code, you have everything you need — no extra packages or API
keys required.

### Basic usage

```bash
# Preview what would be added (no files changed)
python3 scripts/llm_fill_gaps.py --dry-run

# Add up to 30 new entries from the frequency list (default)
python3 scripts/llm_fill_gaps.py

# Add up to 50 new entries from the frequency list
python3 scripts/llm_fill_gaps.py --want 50

# Generate words for a specific topic instead of the frequency list
python3 scripts/llm_fill_gaps.py --topic "months of the year"
python3 scripts/llm_fill_gaps.py --topic "weather and seasons" --dry-run
```

The script:
1. Loads the current dictionary state from `Data/drow_dictionary.db`.
2. Fetches Peter Norvig's English word-frequency list and finds the top `--want`
   words with no coverage.
3. Samples `--samples` (default: 80) random existing pairs from the CSV to use as
   style examples for the LLM.
4. Reads `analysis/corpus_analysis.txt` and `analysis/phoneme_pairs.txt` to give
   the LLM phonotactic context.
5. Calls `claude-opus-4-6` with a structured prompt asking for a CSV block of new
   `english,drow,notes` entries.
6. Collision-checks the response: skips any English word already covered, and any
   Drow form already in the dictionary.
7. Appends accepted rows to `Data/drow_dictionary.csv` (all lowercase).
8. Rebuilds `Data/drow_dictionary.db` from the updated CSV.

### Options

| Flag | Default | Description |
|------|---------|-------------|
| `--want N` | 30 | Number of missing words to request per run (frequency mode) |
| `--samples N` | 80 | Existing pairs shown to LLM as style examples |
| `--topic "..."` | — | Generate words for a topic instead of the frequency list |
| `--dry-run` | off | Print proposed entries without modifying any files |

### After running

Review the printed "Accepted entries" table.  If any generated Drow words look
wrong, remove them directly from `Data/drow_dictionary.csv` and rebuild the DB:

```bash
sqlite3 Data/drow_dictionary.db ".mode csv" ".import Data/drow_dictionary.csv drow_dictionary"
# or use the Python one-liner in the manual workflow below
```

Then run the tests:

```bash
cd drowtest && dotnet run
```

---

## Manual workflow

Use this when you want full control over each generated word.

### 1. Find the gaps

```bash
python3 scripts/missing_words.py
```

Fetches Peter Norvig's English word-frequency list and prints the top 100
most-common English words that have **no entry** in `Data/drow_dictionary.db`.

Re-running after patching will reflect the new additions automatically.

### 2. Filter the list

Many top-ranked words are internet/web-era artifacts (`site`, `click`, `email`,
`dvd`).  Focus on words that are:
- General natural-language vocabulary
- Plausible in a fantasy conversation
- Length ≥ 3 characters, not abbreviations or proper nouns

Also check whether a missing word is just an inflected form of something already
in the dictionary (e.g. `days` → `day` = tangi, or `books` → `book` = voiry).
Those are handled automatically by the morphological pipeline and don't need
explicit entries.

### 3. Generate Drow words

Use the phonological analysis in `analysis/corpus_analysis.txt` and
`analysis/phoneme_pairs.txt` as a guide. Key patterns from the corpus:

**Letters that are Drow-heavy** (use liberally):
`z`, `k`, `l`, `h`, `n`, `r`, `u`

**Letters that are Common-heavy** (use sparingly in Drow words):
`f`, `w`, `p`, `c`

**Characteristic Drow bigrams:**
`th`, `ss`, `el`, `ha`, `la`, `il`, `us`, `na`, `ol`, `ul`, `au`, `sh`, `si`

**Characteristic Drow trigrams** (top occurrences):
`ith`, `har`, `lin`, `rin`, `nth`, `tha`, `uth`, `ess`, `uss`, `ath`, `hal`, `lar`,
`elg`, `ssi`, `vel`, `hin`, `int`, `sin`, `ela`

**Vowel digraphs** that are distinctly Drow:
`au`, `ae`, `ii`, `ua`, `ue`, `ui`, `ei`, `ia`

**Other patterns:**
- Double consonants: `ss`, `rr`, `qu`, `zz`, `nn` are common
- Apostrophes appear in ~30% of Drow words at VC'V boundaries (e.g. `khal'inth`)
- Drow words average ~12% more letters than their English equivalents
- Common endings: `-ith`, `-ath`, `-el`, `-al`, `-in`, `-ar`, `-ul`, `-an`, `-ess`
- Common beginnings: `vel-`, `uss-`, `nil-`, `khal-`, `rin-`, `elg-`, `hal-`, `nar-`

**Semantic derivation** (existing roots to build on):
| Root word | Drow     | Use for compounds |
|-----------|----------|-------------------|
| rule      | ilstar   | management, law, authority |
| plan      | inth     | design, program, scheme |
| learn     | screa    | education, study |
| send      | kus      | mail, post, dispatch |
| keep      | ser      | store, preserve |
| gather    | dryss'ho | forum, assembly |
| secret    | szeous   | privacy, hidden |
| book      | voiry    | library, reading material |
| journey   | z'hind   | travel, trek |

### 4. Check for collisions

Before adding, verify your proposed Drow words don't already exist:

```bash
python3 - <<'EOF'
import sqlite3
conn = sqlite3.connect('Data/drow_dictionary.db')
for w in ['yourword1', 'yourword2']:
    row = conn.execute('SELECT Common FROM drow_dictionary WHERE Drow=?', (w,)).fetchone()
    print(f'{w}: {row[0] if row else "free"}')
EOF
```

### 5. Patch the CSV and rebuild the DB

Append new rows directly to `Data/drow_dictionary.csv` (column order: Drow, Common,
Notes — all lowercase), then rebuild the DB:

```bash
python3 - <<'EOF'
import csv, sqlite3
from pathlib import Path
CSV = Path('Data/drow_dictionary.csv')
DB  = Path('Data/drow_dictionary.db')
with open(CSV, 'a', newline='') as f:
    csv.writer(f, lineterminator='\n').writerow(['drowword', 'english_word', 'note'])
DB.unlink(missing_ok=True)
conn = sqlite3.connect(DB)
conn.execute('CREATE TABLE drow_dictionary (Drow TEXT, Common TEXT, Notes TEXT)')
with open(CSV, newline='') as f:
    conn.executemany('INSERT INTO drow_dictionary VALUES (?,?,?)', csv.reader(f))
conn.commit()
print(conn.execute('SELECT COUNT(*) FROM drow_dictionary').fetchone()[0], 'rows')
EOF
```

**Note:** For synonym mappings (multiple English words → same Drow word), the
automated collision check will block the second entry.  Use the snippet above to
add those manually.

### 6. Run the tests

```bash
cd drowtest && dotnet run
```

All existing tests should still pass.
