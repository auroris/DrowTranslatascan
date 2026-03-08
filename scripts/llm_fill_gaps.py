#!/usr/bin/env python3
"""
LLM-assisted Drow dictionary gap-filling.

Finds common English words missing from the dictionary, asks Claude to invent
Drow translations following the corpus phonotactics, then patches the CSV and
rebuilds the SQLite database.

Usage:
    python3 scripts/llm_fill_gaps.py [--want N] [--samples N] [--dry-run]
    python3 scripts/llm_fill_gaps.py --topic "months of the year" [--dry-run]
    python3 scripts/llm_fill_gaps.py --refresh-queue

Requires:
    Claude CLI installed and authenticated (i.e. `claude` is on your PATH).
"""

import argparse
import csv
import random
import re
import shutil
import sqlite3
import subprocess
import sys
import textwrap
import urllib.request
from pathlib import Path

#  Paths (relative to repo root)
REPO_ROOT    = Path(__file__).parent.parent
CSV_PATH     = REPO_ROOT / "Data" / "drow_dictionary.csv"
DB_PATH      = REPO_ROOT / "Data" / "drow_dictionary.db"
CORPUS_FILE  = REPO_ROOT / "analysis" / "corpus_analysis.txt"
PHONEME_FILE = REPO_ROOT / "analysis" / "phoneme_pairs.txt"
QUEUE_PATH   = Path(__file__).parent / "word_queue.txt"

#  Defaults
FREQ_URL          = "https://norvig.com/ngrams/count_1w.txt"
DEFAULT_WANT      = 100   # words to pull from queue per run
DEFAULT_SAMPLES   = 80    # existing pairs shown to LLM as examples
QUEUE_REFILL_SIZE = 2000  # words to cache when populating the queue
MIN_WORD_LEN      = 3


#  Dictionary helpers

def load_db(db_path: Path) -> tuple[set[str], set[str]]:
    """Return (common_tokens, drow_words) from the DB."""
    conn = sqlite3.connect(db_path)
    rows = conn.execute("SELECT Drow, Common FROM drow_dictionary").fetchall()
    conn.close()
    drow_words  = {r[0].lower() for r in rows}
    common_toks = set()
    for _, common in rows:
        common_toks.update(common.lower().split())
    return common_toks, drow_words


def load_csv_pairs(csv_path: Path) -> list[tuple[str, str, str]]:
    """Return all (drow, common, notes) rows from the CSV."""
    with open(csv_path, newline="", encoding="utf-8") as f:
        return list(csv.reader(f))


def sample_pairs(csv_path: Path, n: int) -> list[tuple[str, str]]:
    """Return n random (common, drow) pairs from the CSV as examples."""
    rows = load_csv_pairs(csv_path)
    sample = random.sample(rows, min(n, len(rows)))
    return [(r[1], r[0]) for r in sample]  # (common, drow)


def read_file_truncated(path: Path, max_chars: int = 4000) -> str:
    """Read a file and truncate to max_chars, appending a note if cut."""
    text = path.read_text(encoding="utf-8")
    if len(text) <= max_chars:
        return text
    return text[:max_chars] + f"\n... [truncated at {max_chars} chars]"


#  Word queue (FIFO cache of frequency-ranked English words)

def load_queue(queue_path: Path) -> list[str]:
    """Return the current queue as an ordered list of words."""
    if not queue_path.exists():
        return []
    return [w for w in queue_path.read_text(encoding="utf-8").splitlines() if w.strip()]


def save_queue(queue_path: Path, words: list[str]) -> None:
    queue_path.write_text("\n".join(words) + ("\n" if words else ""), encoding="utf-8")


def pop_queue(queue_path: Path, n: int) -> tuple[list[str], int]:
    """Remove and return the first n words from the queue.

    Returns (batch, words_remaining_after_pop).
    """
    words = load_queue(queue_path)
    batch = words[:n]
    remaining = words[n:]
    save_queue(queue_path, remaining)
    return batch, len(remaining)


def populate_queue(queue_path: Path, covered: set[str], freq_url: str, size: int) -> int:
    """Fetch the Norvig frequency list, filter already-covered words, and write
    the queue file.  Returns the number of words stored."""
    print(f"Fetching word-frequency list from {freq_url} ...")
    queue: list[str] = []
    with urllib.request.urlopen(freq_url) as resp:
        for line in resp:
            parts = line.decode().rstrip().split("\t")
            if len(parts) != 2:
                continue
            word = parts[0].lower()
            if len(word) < MIN_WORD_LEN:
                continue
            if word not in covered:
                queue.append(word)
            if len(queue) == size:
                break
    save_queue(queue_path, queue)
    return len(queue)


#  Prompt construction

_PREAMBLE = """\
You are helping expand a Drow (Dark Elf) language dictionary for a fantasy
translation app.  The Drow language here is based on the Tel'Mithrim word list
by Brian Sidharta, and also draws on Drow words coined by authors such as
R.A. Salvatore and Elaine Cunningham in the Forgotten Realms setting.  Feel
free to draw on any Drow vocabulary from your training knowledge.  Your task
is to invent plausible new Drow words for English words below, staying true
to the phonological patterns of the corpus."""

_OUTPUT_FORMAT = """\
Return ONLY a fenced CSV block (no other text).  Three columns:
  english, drow, notes
All values lowercase.  Notes: one short phrase explaining the word's
meaning or derivation strategy (10 words max).  Example:

```csv
english,drow,notes
quick,zhaelith,swift movement; -ith ending
cold,narrusk,low temperature; nar- prefix
```

Do not include words that already appear in the examples above.
Do not add any explanation outside the CSV block."""


def _shared_context(
    examples: list[tuple[str, str]],
    corpus_text: str,
    phoneme_text: str,
) -> str:
    """Render the corpus/examples/guidelines block shared by both prompt types."""
    example_lines = "\n".join(
        f"  {common:<25} - {drow}" for common, drow in examples
    )
    return f"""\
        -
        CORPUS ANALYSIS (letter/bigram/trigram statistics)
        -
        {corpus_text}

        -
        PHONEME-PAIR EXAMPLES (existing English-Drow mappings)
        -
        {phoneme_text}

        -
        RANDOM SAMPLE OF EXISTING DICTIONARY PAIRS
        -
        (English - Drow)
        {example_lines}

        -
        PHONOTACTIC GUIDELINES (summary)
        -
        • Drow-heavy letters: z, k, l, h, n, r, u  - use liberally
        • Common-heavy letters: f, w, p, c - use sparingly
        • Strong Drow bigrams: th, ss, el, ha, la, il, us, na, ol, ul, au, sh, si
        • Strong Drow trigrams: ith, har, lin, rin, uth, ess, uss, ath, hal, elg, vel
        • Vowel digraphs characteristic of Drow: au, ae, ii, ua, ue, ui, ei, ia
        • Double consonants: ss, rr, qu, zz, nn are common
        • Apostrophes at VC'V boundaries in ~30 % of words (e.g. khal'inth, z'hind)
        • Prefer short Drow forms (2-7 letters); resist the urge to make words longer
        • Drow words average ~12 % more letters than English equivalents, but
          brevity is more important than following that average
        • Common endings: -ith, -ath, -el, -al, -in, -ar, -ul, -an, -ess
        • Common prefixes: vel-, uss-, nil-, khal-, rin-, elg-, hal-, nar-
        • Avoid Common-language patterns: -ing, -tion, -ed, -er, -ness
        • Each invented word must be unique - do not reuse a Drow form"""


def build_prompt(
    candidates: list[str],
    examples: list[tuple[str, str]],
    corpus_text: str,
    phoneme_text: str,
) -> str:
    word_list = "\n".join(f"  {w}" for w in candidates)
    context = _shared_context(examples, corpus_text, phoneme_text)

    return textwrap.dedent(f"""
        {_PREAMBLE}

        {context}

        -
        CANDIDATE ENGLISH WORDS
        -
        The list below was drawn from a general English frequency list and may include
        modern or anachronistic words (e.g. "computer", "email", "website") that have
        no place in a fantasy setting.  Simply omit any such word from your output --
        the app has an algorithmic fallback for modern vocabulary.  Translate only the
        words that are natural in a pre-modern fantasy context.

        {word_list}

        -
        OUTPUT FORMAT
        -
        {_OUTPUT_FORMAT}
    """).strip()


def build_topic_prompt(
    topic: str,
    examples: list[tuple[str, str]],
    corpus_text: str,
    phoneme_text: str,
    covered_common: set[str],
) -> str:
    already = ", ".join(sorted(covered_common)[:300])  # cap to keep prompt size sane
    context = _shared_context(examples, corpus_text, phoneme_text)

    return textwrap.dedent(f"""
        {_PREAMBLE}

        {context}

        -
        TOPIC: {topic}
        -
        Enumerate all English words that belong to this topic (nouns, adjectives,
        verbs - whatever is natural for the topic), then invent a Drow translation
        for each one.  Skip any word that appears in this already-covered list:

        {already}

        -
        OUTPUT FORMAT
        -
        {_OUTPUT_FORMAT}
    """).strip()


#  Response parsing

def parse_llm_csv(text: str) -> list[tuple[str, str, str]]:
    """Extract and parse the CSV block from the LLM response."""
    match = re.search(r"```csv\s*\n(.*?)```", text, re.DOTALL | re.IGNORECASE)
    if not match:
        match = re.search(r"(english,drow,notes\n.*)", text, re.DOTALL | re.IGNORECASE)
    if not match:
        sys.exit(
            "ERROR: Could not find a CSV block in the LLM response.\n"
            "Raw response:\n" + text
        )
    raw = match.group(1).strip()
    reader = csv.DictReader(raw.splitlines())
    rows = []
    for row in reader:
        english = row.get("english", "").strip().lower()
        drow    = row.get("drow",    "").strip().lower()
        notes   = row.get("notes",   "").strip().lower()
        if english and drow:
            rows.append((english, drow, notes or "NULL"))
    return rows


#  Collision checking

def collision_check(
    proposed: list[tuple[str, str, str]],
    covered_common: set[str],
    covered_drow:   set[str],
) -> tuple[list[tuple[str, str, str]], list[str]]:
    """Split proposed rows into (accepted, skipped_reasons).

    Skips if the English word is already covered OR the Drow word is already used.
    Deduplicates within the proposed list itself.
    """
    accepted: list[tuple[str, str, str]] = []
    skipped:  list[str] = []
    seen_drow:    set[str] = set(covered_drow)
    seen_english: set[str] = set(covered_common)

    for english, drow, notes in proposed:
        if english in seen_english:
            skipped.append(f"  SKIP  {english:<25} - English already covered")
        elif drow in seen_drow:
            skipped.append(f"  SKIP  {english:<25} - {drow}  - Drow collision")
        else:
            accepted.append((english, drow, notes))
            seen_drow.add(drow)
            seen_english.update(english.split())

    return accepted, skipped


#  Patching

def append_to_csv(rows: list[tuple[str, str, str]], csv_path: Path) -> None:
    """Append (english, drow, notes) rows to the CSV as (drow, english, notes)."""
    with open(csv_path, "a", newline="", encoding="utf-8") as f:
        writer = csv.writer(f, lineterminator="\n")
        for english, drow, notes in rows:
            writer.writerow([drow, english, notes or "NULL"])


def rebuild_db(csv_path: Path, db_path: Path) -> int:
    """Rebuild the SQLite DB from the CSV; return row count."""
    db_path.unlink(missing_ok=True)
    conn = sqlite3.connect(db_path)
    conn.execute(
        "CREATE TABLE drow_dictionary (Drow TEXT, Common TEXT, Notes TEXT)"
    )
    with open(csv_path, newline="", encoding="utf-8") as f:
        conn.executemany(
            "INSERT INTO drow_dictionary VALUES (?,?,?)", csv.reader(f)
        )
    conn.commit()
    count = conn.execute("SELECT COUNT(*) FROM drow_dictionary").fetchone()[0]
    conn.close()
    return count


#  Main

def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--want", type=int, default=DEFAULT_WANT,
        help=f"Words to pull from the queue per run (default: {DEFAULT_WANT})",
    )
    parser.add_argument(
        "--samples", type=int, default=DEFAULT_SAMPLES,
        help=f"Existing pairs shown to the LLM as examples (default: {DEFAULT_SAMPLES})",
    )
    parser.add_argument(
        "--topic",
        help='Generate words for a topic instead of the queue (e.g. "months of the year")',
    )
    parser.add_argument(
        "--refresh-queue", action="store_true",
        help="Re-fetch the frequency list and repopulate the local word queue, then exit",
    )
    parser.add_argument(
        "--dry-run", action="store_true",
        help="Print proposed entries but do not modify any files",
    )
    args = parser.parse_args()

    if not shutil.which("claude"):
        sys.exit("ERROR: 'claude' CLI not found on PATH. Install Claude Code and log in.")

    # 1. Load current dictionary state
    print(f"Loading dictionary from {DB_PATH} ...")
    covered_common, covered_drow = load_db(DB_PATH)
    print(f"  {len(covered_common):,} Common tokens, {len(covered_drow):,} Drow words\n")

    # 2. Handle --refresh-queue
    if args.refresh_queue:
        count = populate_queue(QUEUE_PATH, covered_common, FREQ_URL, QUEUE_REFILL_SIZE)
        print(f"Queue repopulated: {count} words written to {QUEUE_PATH}")
        return

    # 3. Load shared context
    examples     = sample_pairs(CSV_PATH, args.samples)
    corpus_text  = read_file_truncated(CORPUS_FILE)
    phoneme_text = read_file_truncated(PHONEME_FILE, max_chars=2000)

    # 4. Build prompt
    if args.topic:
        print(f"Topic mode: \"{args.topic}\"\n")
        prompt = build_topic_prompt(
            args.topic, examples, corpus_text, phoneme_text, covered_common
        )
        print("Calling Claude CLI to generate topic words ...\n")
    else:
        # Populate queue on first run or if exhausted
        queue = load_queue(QUEUE_PATH)
        if not queue:
            print(f"Word queue is empty, populating from frequency list ...")
            count = populate_queue(QUEUE_PATH, covered_common, FREQ_URL, QUEUE_REFILL_SIZE)
            print(f"  Cached {count} words to {QUEUE_PATH}\n")
            queue = load_queue(QUEUE_PATH)

        candidates, remaining = pop_queue(QUEUE_PATH, args.want)
        print(f"Pulled {len(candidates)} words from queue ({remaining} remaining).\n")
        if not args.dry_run:
            # Queue already updated on disk by pop_queue
            pass

        prompt = build_prompt(candidates, examples, corpus_text, phoneme_text)
        print(f"Calling Claude CLI to translate up to {len(candidates)} words ...\n")

    # 5. Call the Claude CLI
    result = subprocess.run(
        ["claude", "-p", prompt],
        capture_output=True,
        text=True,
    )
    if result.returncode != 0:
        sys.exit(f"ERROR: claude CLI exited with code {result.returncode}:\n{result.stderr}")
    raw_response = result.stdout

    # 6. Parse response
    proposed = parse_llm_csv(raw_response)
    print(f"LLM proposed {len(proposed)} entries.\n")

    # 7. Collision check
    accepted, skipped = collision_check(proposed, covered_common, covered_drow)

    if skipped:
        print("Skipped entries:")
        for line in skipped:
            print(line)
        print()

    if not accepted:
        print("No new entries to add after collision filtering.")
        return

    print("Accepted entries:")
    print(f"  {'English':<25}  {'Drow':<25}  Notes")
    print(f"  {'-'*25}  {'-'*25}  -----")
    for english, drow, notes in accepted:
        print(f"  {english:<25}  {drow:<25}  {notes}")
    print()

    if args.dry_run:
        print("--dry-run: no files were modified.")
        return

    # 8. Patch CSV and rebuild DB
    print(f"Appending {len(accepted)} rows to {CSV_PATH} ...")
    append_to_csv(accepted, CSV_PATH)

    print(f"Rebuilding {DB_PATH} ...")
    total = rebuild_db(CSV_PATH, DB_PATH)
    print(f"Done -- {total:,} rows in dictionary.")


if __name__ == "__main__":
    main()
