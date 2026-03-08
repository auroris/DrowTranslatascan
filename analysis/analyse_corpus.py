#!/usr/bin/env python3
"""
Corpus analysis: compare character/bigram/phoneme frequencies between
the Drow and Common columns of drow_dictionary.db to identify Drow-isms
that should inform a phoneme cipher.
"""

import sqlite3
import re
from collections import Counter
from pathlib import Path

DB = Path(__file__).parent.parent / "tel'mithrim" / "drow_dictionary.db"

# ── Load corpus ────────────────────────────────────────────────────────────────

with sqlite3.connect(DB) as con:
    rows = con.execute("SELECT Drow, Common FROM drow_dictionary").fetchall()

drow_words   = [r[0].lower() for r in rows if r[0] and r[1]]
common_words = [r[1].lower() for r in rows if r[0] and r[1]]

print(f"Loaded {len(drow_words)} pairs\n")

# ── Helper ─────────────────────────────────────────────────────────────────────

def letter_freq(words):
    c = Counter()
    for w in words:
        c.update(ch for ch in w if ch.isalpha())
    total = sum(c.values())
    return {k: v / total for k, v in c.most_common()}

def bigram_freq(words):
    c = Counter()
    for w in words:
        letters = [ch for ch in w if ch.isalpha()]
        for a, b in zip(letters, letters[1:]):
            c[a + b] += 1
    total = sum(c.values())
    return {k: v / total for k, v in c.most_common(40)}

def apostrophe_stats(words):
    """Position of apostrophes relative to word length."""
    positions = []
    for w in words:
        for i, ch in enumerate(w):
            if ch == "'":
                positions.append(i / max(len(w) - 1, 1))
    return positions

# ── 1. Letter frequencies ──────────────────────────────────────────────────────

df = letter_freq(drow_words)
cf = letter_freq(common_words)

all_letters = sorted(set(df) | set(cf))

print("=== Letter frequencies (Drow vs Common, ratio Drow/Common) ===")
print(f"{'Letter':>6}  {'Drow%':>6}  {'Common%':>8}  {'Ratio':>6}")
ratios = []
for ch in all_letters:
    d = df.get(ch, 0)
    c = cf.get(ch, 0)
    ratio = (d / c) if c > 0 else float('inf')
    ratios.append((ch, d, c, ratio))

for ch, d, c, ratio in sorted(ratios, key=lambda x: -x[3]):
    marker = " ◄ Drow-heavy" if ratio > 1.5 else (" ◄ Common-heavy" if ratio < 0.5 else "")
    print(f"  {ch:>4}  {d*100:>6.2f}%  {c*100:>8.2f}%  {ratio:>6.2f}{marker}")

# ── 2. Bigram frequencies ──────────────────────────────────────────────────────

db = bigram_freq(drow_words)
cb = bigram_freq(common_words)

print("\n=== Top 40 Drow bigrams ===")
for bg, freq in db.items():
    ce = cb.get(bg, 0)
    ratio = (freq / ce) if ce > 0 else float('inf')
    marker = " ◄" if ratio > 2 else ""
    print(f"  {bg}  {freq*100:.2f}%  (Common: {ce*100:.2f}%  ratio: {ratio:.1f}){marker}")

print("\n=== Top 40 Common bigrams not prominent in Drow ===")
for bg, freq in sorted(cb.items(), key=lambda x: -x[1]):
    de = db.get(bg, 0)
    ratio = (de / freq) if freq > 0 else 0
    if ratio < 0.4:
        print(f"  {bg}  Common: {freq*100:.2f}%  Drow: {de*100:.2f}%  ratio: {ratio:.2f}")

# ── 3. Drow-specific clusters / patterns ──────────────────────────────────────

print("\n=== Drow-specific multi-char patterns (not preceded by apostrophe) ===")
patterns = ["ii", "yr", "ae", "au", "ss", "rr", "qu", "zz", "nn", "ll", "tt"]
for p in patterns:
    dc = sum(1 for w in drow_words if p in w)
    cc = sum(1 for w in common_words if p in w)
    print(f"  {p}  in {dc} Drow words ({dc/len(drow_words)*100:.1f}%)  vs {cc} Common ({cc/len(common_words)*100:.1f}%)")

# ── 4. Apostrophe usage ────────────────────────────────────────────────────────

drow_apos = sum(w.count("'") for w in drow_words)
common_apos = sum(w.count("'") for w in common_words)
drow_letters = sum(sum(c.isalpha() for c in w) for w in drow_words)
common_letters = sum(sum(c.isalpha() for c in w) for w in common_words)

print(f"\n=== Apostrophe density ===")
print(f"  Drow:   {drow_apos} apostrophes in {drow_letters} letters  ({drow_apos/drow_letters*100:.2f}%)")
print(f"  Common: {common_apos} apostrophes in {common_letters} letters ({common_apos/common_letters*100:.2f}%)")

drow_with_apos = sum(1 for w in drow_words if "'" in w)
print(f"  Drow words containing apostrophe: {drow_with_apos}/{len(drow_words)} ({drow_with_apos/len(drow_words)*100:.1f}%)")

# ── 5. Word length comparison ──────────────────────────────────────────────────

drow_len   = [len(re.sub(r"[^a-z]", "", w)) for w in drow_words]
common_len = [len(re.sub(r"[^a-z]", "", w)) for w in common_words]

print(f"\n=== Word length (letters only) ===")
print(f"  Drow:   avg {sum(drow_len)/len(drow_len):.2f}  min {min(drow_len)}  max {max(drow_len)}")
print(f"  Common: avg {sum(common_len)/len(common_len):.2f}  min {min(common_len)}  max {max(common_len)}")

ratio_lengths = [d/c if c else 0 for d, c in zip(drow_len, common_len)]
print(f"  Drow/Common letter ratio: avg {sum(ratio_lengths)/len(ratio_lengths):.2f}")

# ── 6. Vowel/consonant balance ─────────────────────────────────────────────────

VOWELS = set("aeiou")

def vc_ratio(words):
    v = c = 0
    for w in words:
        for ch in w:
            if ch.isalpha():
                if ch in VOWELS: v += 1
                else: c += 1
    return v / (v + c)

print(f"\n=== Vowel/consonant balance ===")
print(f"  Drow:   {vc_ratio(drow_words)*100:.1f}% vowels")
print(f"  Common: {vc_ratio(common_words)*100:.1f}% vowels")

# ── 7. Common vowel sequences ─────────────────────────────────────────────────

def vowel_sequences(words):
    c = Counter()
    for w in words:
        for m in re.finditer(r'[aeiou]+', re.sub(r"[^a-z]", "", w)):
            if len(m.group()) >= 2:
                c[m.group()] += 1
    return c.most_common(20)

print(f"\n=== Top vowel sequences (2+) in Drow ===")
for seq, cnt in vowel_sequences(drow_words):
    cc = sum(1 for w in common_words if seq in re.sub(r"[^a-z]", "", w))
    print(f"  {seq:<8} {cnt:>4}x in Drow words  /  {cc} Common words contain it")

print(f"\n=== Top vowel sequences (2+) in Common ===")
for seq, cnt in vowel_sequences(common_words):
    dc = sum(1 for w in drow_words if seq in re.sub(r"[^a-z]", "", w))
    print(f"  {seq:<8} {cnt:>4}x  /  {dc} Drow words contain it")

# ── 8. Word-initial and word-final patterns ────────────────────────────────────

def boundary_ngrams(words, n, position):
    """Count n-grams at the start ('initial') or end ('final') of each word."""
    c = Counter()
    for w in words:
        letters = re.sub(r"[^a-z]", "", w)
        if len(letters) >= n:
            gram = letters[:n] if position == "initial" else letters[-n:]
            c[gram] += 1
    total = len(words)
    return [(gram, count, count / total) for gram, count in c.most_common(12)]

for n, label in [(1, "letter"), (2, "bigram")]:
    print(f"\n=== Top 12 Drow word-initial {label}s ===")
    for gram, count, pct in boundary_ngrams(drow_words, n, "initial"):
        print(f"  {gram:<6}  {count:>4} words  ({pct*100:.1f}%)")

    print(f"\n=== Top 12 Drow word-final {label}s ===")
    for gram, count, pct in boundary_ngrams(drow_words, n, "final"):
        print(f"  {gram:<6}  {count:>4} words  ({pct*100:.1f}%)")

# ── 9. Word-length distribution ────────────────────────────────────────────────

print(f"\n=== Drow word-length distribution (letters only) ===")
length_counts = Counter(drow_len)
for length in sorted(length_counts):
    bar = "#" * (length_counts[length] // 10)
    print(f"  {length:>2} letters: {length_counts[length]:>4} words  {bar}")

# ── 10. Common Drow prefixes and suffixes (data-driven) ───────────────────────

print(f"\n=== Top 10 Drow word prefixes (2- and 3-char) ===")
for n in (2, 3):
    print(f"  {n}-char:")
    for gram, count, pct in boundary_ngrams(drow_words, n, "initial")[:10]:
        print(f"    {gram:<6}  {count:>4} words  ({pct*100:.1f}%)")

print(f"\n=== Top 10 Drow word suffixes (2- and 3-char) ===")
for n in (2, 3):
    print(f"  {n}-char:")
    for gram, count, pct in boundary_ngrams(drow_words, n, "final")[:10]:
        print(f"    {gram:<6}  {count:>4} words  ({pct*100:.1f}%)")
