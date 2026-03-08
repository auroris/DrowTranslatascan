#!/usr/bin/env python3
"""
Find the top 100 most-common English words that have no entry in the Drow dictionary.

Word frequency source: Peter Norvig's count_1w.txt, derived from Google n-grams.
  https://norvig.com/ngrams/count_1w.txt
"""

import sqlite3
import urllib.request

FREQ_URL = "https://norvig.com/ngrams/count_1w.txt"
DB_PATH = "Data/drow_dictionary.db"
WANT = 100
# Single letters are mostly noise (index variables, abbreviations, etc.)
MIN_LENGTH = 2


def load_drow_common_words(db_path: str) -> set[str]:
    """Return the set of all lowercased Common words in the dictionary."""
    conn = sqlite3.connect(db_path)
    rows = conn.execute("SELECT lower(Common) FROM drow_dictionary").fetchall()
    conn.close()
    # Each Common cell may be a multi-word phrase; include every word in it so
    # that "i leave you" also covers "leave" and "you" individually.
    words = set()
    for (cell,) in rows:
        words.update(cell.split())
    return words


def iter_freq_list(url: str):
    """Yield (word, rank) from the Norvig frequency list in frequency order."""
    with urllib.request.urlopen(url) as resp:
        for rank, line in enumerate(resp, start=1):
            parts = line.decode().rstrip().split("\t")
            if len(parts) == 2:
                yield parts[0].lower(), rank


def main():
    print(f"Loading Drow dictionary from {DB_PATH} …")
    covered = load_drow_common_words(DB_PATH)
    print(f"  {len(covered):,} unique Common tokens found.\n")

    print(f"Fetching word-frequency list from {FREQ_URL} …\n")
    missing = []
    for word, rank in iter_freq_list(FREQ_URL):
        if len(word) < MIN_LENGTH:
            continue
        if word not in covered:
            missing.append((rank, word))
        if len(missing) == WANT:
            break

    print(f"Top {WANT} common English words missing from the Drow dictionary:\n")
    print(f"  {'Rank':>6}  Word")
    print(f"  {'------':>6}  ----")
    for rank, word in missing:
        print(f"  {rank:>6}  {word}")


if __name__ == "__main__":
    main()
