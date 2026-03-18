#!/usr/bin/env python3
"""
Data processor for sample-project-1
Processes CSV files and outputs statistics.
"""

import csv
import sys
from pathlib import Path


def process_file(filepath):
    """Read CSV and compute basic statistics."""
    if not Path(filepath).exists():
        print(f"Error: {filepath} not found")
        return

    with open(filepath, 'r') as f:
        reader = csv.DictReader(f)
        rows = list(reader)

    print(f"Processed {len(rows)} rows from {filepath}")
    print("Columns:", list(rows[0].keys()) if rows else "N/A")


if __name__ == "__main__":
    if len(sys.argv) > 1:
        process_file(sys.argv[1])
    else:
        print("Usage: python data-processor.py <csvfile>")
