#!/usr/bin/env python3
"""
Patch drow_dictionary.csv with Drow translations for high-frequency English words
that had no dictionary entry.

Words selected from the Norvig top-1000 list after filtering out internet/modern
jargon. Drow forms were derived from corpus phonotactics (analysis/corpus_analysis.txt):
  - Heavy use of: z, k, l, h, n, r, au, ae, ii, ss, qu, th
  - Common Drow trigrams: ith, har, lin, rin, nth, tha, uss, ess, ath, vel
  - Apostrophes at VC'V boundaries (~30 % of words)
  - Words run ~12 % longer than their English equivalents
"""

import csv
import sqlite3
from pathlib import Path

ROOT    = Path(__file__).parent.parent
CSV     = ROOT / "Data" / "drow_dictionary.csv"
DB      = ROOT / "Data" / "drow_dictionary.db"

# (common, drow, notes)
NEW_ENTRIES = [
    # ── Core verbs / actions ──────────────────────────────────────────────────
    ("read",        "velithar",     "to read/decipher text"),
    ("view",        "sinveth",      "to see/observe"),
    ("check",       "elgath",       "to inspect/verify"),
    ("add",         "urqueth",      "to include/add to"),
    ("buy",         "khalath",      "to purchase/acquire"),
    ("sell",        "khalveth",     "to trade away"),
    ("store",       "serith",       "to keep/hoard"),
    ("travel",      "z'hind",       "to journey (see also: journey)"),
    ("said",        "thessar",      "past of say/speak"),
    # ── Nouns: commerce & administration ─────────────────────────────────────
    ("news",        "thalorin",     "tidings/news"),
    ("price",       "thalveth",     "value/cost in trade"),
    ("trade",       "khalven",      "commerce/barter"),
    ("list",        "rinluth",      "catalog/record"),
    ("number",      "thallar",      "count/tally"),
    ("mail",        "kussiven",     "sent message (kus=send)"),
    ("post",        "kussinel",     "dispatch/station (kus=send)"),
    ("service",     "ussrel",       "duty rendered"),
    ("report",      "thessan",      "account/telling"),
    ("terms",       "quellar",      "conditions/terms"),
    ("code",        "quelath",      "law/cipher"),
    ("program",     "intharel",     "elaborate plan (inth=plan)"),
    ("design",      "khal'inth",    "crafted plan/scheme"),
    ("project",     "khal'inthus",  "major undertaking"),
    ("management",  "ilstaren",     "administration (ilstar=rule)"),
    ("education",   "screavel",     "the act of learning (screa=learn)"),
    ("law",         "ilstarel",     "the rules (ilstar=rule)"),
    ("office",      "nalthess",     "position/station of authority"),
    ("date",        "nalthur",      "a point in time"),
    ("line",        "rintal",       "a row/boundary"),
    ("case",        "rinthar",      "instance/matter at hand"),
    ("forum",       "dryss'el",     "place of gathering (dryss'ho=gather)"),
    ("subject",     "khalithar",    "one under rule; a topic"),
    # ── Nouns: society & people ───────────────────────────────────────────────
    ("public",      "nalaur",       "the common people/open"),
    ("privacy",     "szarith",      "secrecy/seclusion (szeous=secret)"),
    ("community",   "ussilan",      "group of kin"),
    ("local",       "harithal",     "of this place/nearby"),
    ("women",       "j'nesstin",    "plural of j'nesst (woman)"),
    ("special",     "elgauss",      "particular/chosen"),
    ("sign",        "ussael",       "symbol/omen"),
    ("review",      "ilthuss",      "examination/re-inspection"),
]


def load_existing_pairs(db: Path):
    conn = sqlite3.connect(db)
    common_set = {r[0].lower() for r in conn.execute("SELECT Common FROM drow_dictionary")}
    drow_set   = {r[0].lower() for r in conn.execute("SELECT Drow   FROM drow_dictionary")}
    conn.close()
    return common_set, drow_set


def main():
    common_set, drow_set = load_existing_pairs(DB)

    to_add = []
    for common, drow, notes in NEW_ENTRIES:
        if common.lower() in common_set:
            print(f"  SKIP  (common already exists)  {common} -> {drow}")
        elif drow.lower() in drow_set:
            print(f"  SKIP  (drow collision)          {common} -> {drow}")
        else:
            to_add.append((drow, common, notes))
            print(f"  ADD   {common:20} -> {drow}")

    if not to_add:
        print("\nNothing to add.")
        return

    # Append to CSV (already-lowercase, consistent with rest of file)
    with open(CSV, "a", newline="") as f:
        w = csv.writer(f, lineterminator="\n")
        for drow, common, notes in to_add:
            w.writerow([drow.lower(), common.lower(), notes])

    print(f"\nAppended {len(to_add)} rows to {CSV.name}.")

    # Rebuild DB
    print(f"Rebuilding {DB.name} …")
    import os
    DB.unlink(missing_ok=True)
    conn = sqlite3.connect(DB)
    conn.execute("CREATE TABLE drow_dictionary (Drow TEXT, Common TEXT, Notes TEXT)")
    with open(CSV, newline="") as f:
        conn.executemany("INSERT INTO drow_dictionary VALUES (?,?,?)", csv.reader(f))
    conn.commit()
    total = conn.execute("SELECT COUNT(*) FROM drow_dictionary").fetchone()[0]
    conn.close()
    print(f"Done — {total:,} rows in database.")


if __name__ == "__main__":
    main()
