# Dictionary Gap-Filling Workflow

Use these scripts when you want to expand the Drow dictionary with translations for
common English words that are currently missing.

## Steps

### 1. Find the gaps

```bash
python3 scripts/missing_words.py
```

Fetches Peter Norvig's English word-frequency list (Google n-gram derived) and prints
the top 100 most-common English words that have **no entry** in `Data/drow_dictionary.db`.

Re-running after patching will reflect the new additions automatically.

### 2. Filter the list

Many top-ranked words are internet/web-era artifacts (`site`, `click`, `email`, `dvd`).
Focus on words that are:
- General natural-language vocabulary
- Plausible in a fantasy conversation
- Length ≥ 3 characters, not abbreviations or proper nouns

Also check whether a missing word is just an inflected form of something already in the
dictionary (e.g. `days` → `day` = tangi, or `books` → `book` = voiry). Those are handled
automatically by the morphological pipeline and don't need explicit entries.

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
| Root word | Drow    | Use for compounds |
|-----------|---------|-------------------|
| rule      | ilstar  | management, law, authority |
| plan      | inth    | design, program, scheme |
| learn     | screa   | education, study |
| send      | kus     | mail, post, dispatch |
| keep      | ser     | store, preserve |
| gather    | dryss'ho| forum, assembly |
| secret    | szeous  | privacy, hidden |
| book      | voiry   | library, reading material |
| journey   | z'hind  | travel, trek |

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

Edit `scripts/patch_missing_words.py` and add your new entries to `NEW_ENTRIES`:

```python
NEW_ENTRIES = [
    # (common_english, drow_word, notes)
    ("yourword", "drowform", "brief meaning note"),
    ...
]
```

Then run:

```bash
python3 scripts/patch_missing_words.py
```

The script skips entries where the Common word already exists, checks for Drow-side
collisions, appends the new rows to the CSV (all lowercase), and rebuilds the DB.

**Note:** For synonym mappings (multiple English words → same Drow word), the collision
check will block the second entry. Add those manually:

```bash
python3 - <<'EOF'
import csv, sqlite3
from pathlib import Path
CSV = Path('Data/drow_dictionary.csv')
DB  = Path('Data/drow_dictionary.db')
with open(CSV, 'a', newline='') as f:
    csv.writer(f, lineterminator='\n').writerow(['drowword', 'english_synonym', 'note'])
DB.unlink(missing_ok=True)
conn = sqlite3.connect(DB)
conn.execute('CREATE TABLE drow_dictionary (Drow TEXT, Common TEXT, Notes TEXT)')
with open(CSV, newline='') as f:
    conn.executemany('INSERT INTO drow_dictionary VALUES (?,?,?)', csv.reader(f))
conn.commit()
print(conn.execute('SELECT COUNT(*) FROM drow_dictionary').fetchone()[0], 'rows')
EOF
```

### 6. Run the tests

```bash
cd drowtest && dotnet run
```

All existing tests should still pass. Consider adding spot-check assertions for a few
of the new words to the test suite.
