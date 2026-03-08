#!/usr/bin/env python3
"""
Reset Data/drow_dictionary.csv from the upstream tel'mithrim source, normalizing
all Drow and Common values to lowercase, then rebuild Data/drow_dictionary.db.

Usage:
    python3 scripts/reset_dictionary.py
"""

import csv
import sqlite3
from pathlib import Path

REPO_ROOT  = Path(__file__).parent.parent
SOURCE_CSV = REPO_ROOT / "tel'mithrim" / "drow_dictionary.csv"
TARGET_CSV = REPO_ROOT / "Data" / "drow_dictionary.csv"
TARGET_DB  = REPO_ROOT / "Data" / "drow_dictionary.db"


def reset_csv(source: Path, target: Path) -> int:
    """Copy source → target with all fields lowercased. Returns row count."""
    with open(source, newline="", encoding="utf-8") as src, \
         open(target, "w", newline="", encoding="utf-8") as dst:
        writer = csv.writer(dst, lineterminator="\n")
        for row in csv.reader(src):
            writer.writerow([field.lower() for field in row])
    return sum(1 for _ in open(target, encoding="utf-8").readlines())


def rebuild_db(csv_path: Path, db_path: Path) -> int:
    """Rebuild the SQLite DB from the CSV. Returns row count."""
    db_path.unlink(missing_ok=True)
    conn = sqlite3.connect(db_path)
    conn.execute("CREATE TABLE drow_dictionary (Drow TEXT, Common TEXT, Notes TEXT)")
    with open(csv_path, newline="", encoding="utf-8") as f:
        conn.executemany("INSERT INTO drow_dictionary VALUES (?,?,?)", csv.reader(f))
    conn.commit()
    count = conn.execute("SELECT COUNT(*) FROM drow_dictionary").fetchone()[0]
    conn.close()
    return count


def main() -> None:
    print(f"Source: {SOURCE_CSV}")
    print(f"Target: {TARGET_CSV}")
    print()

    rows = reset_csv(SOURCE_CSV, TARGET_CSV)
    print(f"Wrote {rows} rows to {TARGET_CSV} (all lowercase).")

    count = rebuild_db(TARGET_CSV, TARGET_DB)
    print(f"Rebuilt {TARGET_DB} — {count} rows.")


if __name__ == "__main__":
    main()
