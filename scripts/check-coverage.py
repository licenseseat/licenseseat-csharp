#!/usr/bin/env python3
"""Fail when a Cobertura report falls below the repository's coverage floor."""

from __future__ import annotations

import argparse
from decimal import Decimal, InvalidOperation
from pathlib import Path
import sys
import xml.etree.ElementTree as ET


def percentage(value: str, field: str) -> Decimal:
    try:
        rate = Decimal(value)
    except InvalidOperation as error:
        raise ValueError(f"Cobertura {field} is not numeric") from error

    if rate < 0 or rate > 1:
        raise ValueError(f"Cobertura {field} is outside [0, 1]")

    return rate * 100


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("report", type=Path)
    parser.add_argument("--minimum-lines", type=Decimal, default=Decimal("80"))
    parser.add_argument("--minimum-branches", type=Decimal, default=Decimal("70"))
    args = parser.parse_args()

    if not args.report.is_file():
        parser.error(f"coverage report does not exist: {args.report}")

    if not 0 <= args.minimum_lines <= 100 or not 0 <= args.minimum_branches <= 100:
        parser.error("coverage thresholds must be between 0 and 100")

    try:
        root = ET.parse(args.report).getroot()
        if root.tag != "coverage":
            raise ValueError("document root is not <coverage>")
        line_rate = percentage(root.attrib["line-rate"], "line-rate")
        branch_rate = percentage(root.attrib["branch-rate"], "branch-rate")
    except (ET.ParseError, KeyError, ValueError) as error:
        print(f"Invalid Cobertura report: {error}", file=sys.stderr)
        return 2

    print(f"Line coverage:   {line_rate:.2f}% (minimum {args.minimum_lines:.2f}%)")
    print(f"Branch coverage: {branch_rate:.2f}% (minimum {args.minimum_branches:.2f}%)")

    failed = False
    if line_rate < args.minimum_lines:
        print("Line coverage is below the enforced floor.", file=sys.stderr)
        failed = True
    if branch_rate < args.minimum_branches:
        print("Branch coverage is below the enforced floor.", file=sys.stderr)
        failed = True
    return 1 if failed else 0


if __name__ == "__main__":
    raise SystemExit(main())
