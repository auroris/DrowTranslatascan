#!/usr/bin/env python3
"""
Investigates specific phoneme/grapheme correspondences in dictionary pairs
to inform the bijective cipher design.
"""

import sqlite3, re
from collections import Counter
from pathlib import Path

DB = Path(__file__).parent.parent / "Data" / "drow_dictionary.db"

with sqlite3.connect(DB) as con:
    rows = con.execute("SELECT Drow, Common FROM drow_dictionary").fetchall()

drow_words   = [r[0].lower() for r in rows if r[0] and r[1]]
common_words = [r[1].lower() for r in rows if r[0] and r[1]]

def show_examples(label, pairs, n=15):
    print(f"\n'{label}' — {len(pairs)} pairs. Examples:")
    for d, c in pairs[:n]:
        print(f"  {c:25} → {d}")

# j in Drow
show_examples("j in Drow", [(d, c) for d, c in zip(drow_words, common_words) if 'j' in d])

# w in Drow
show_examples("w in Drow", [(d, c) for d, c in zip(drow_words, common_words) if 'w' in d])

# p in Drow vs Common
p_drow   = sum(1 for w in drow_words   if 'p' in w)
p_common = sum(1 for w in common_words if 'p' in w)
print(f"\n'p': {p_drow}/{len(drow_words)} Drow words  vs  {p_common}/{len(common_words)} Common words")

# qu in Drow
show_examples("qu in Drow", [(d, c) for d, c in zip(drow_words, common_words) if 'qu' in d])

# z in Common
show_examples("z in Common (Drow side)", [(d, c) for d, c in zip(drow_words, common_words) if 'z' in c])

# Letters in Drow that never appear in Common
drow_only_chars = set()
for ch in 'abcdefghijklmnopqrstuvwxyz':
    d_count = sum(1 for w in drow_words if ch in w)
    c_count = sum(1 for w in common_words if ch in w)
    if d_count > 0 and c_count == 0:
        drow_only_chars.add(ch)
print(f"\nChars present in Drow words but never in Common words: {sorted(drow_only_chars)}")

# Letters in Common that never appear in Drow
for ch in 'abcdefghijklmnopqrstuvwxyz':
    d_count = sum(1 for w in drow_words if ch in w)
    c_count = sum(1 for w in common_words if ch in w)
    if c_count > 0 and d_count == 0:
        print(f"  '{ch}' appears in Common but never in Drow")

# Trigram analysis of Drow
tri = Counter()
for w in drow_words:
    letters = [c for c in w if c.isalpha()]
    for a, b, c in zip(letters, letters[1:], letters[2:]):
        tri[a+b+c] += 1
print(f"\nTop 20 Drow trigrams:")
for t, n in tri.most_common(20):
    print(f"  {t}  {n}x")
