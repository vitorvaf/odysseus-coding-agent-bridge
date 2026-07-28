#!/usr/bin/env python3
"""
Verify that a TRX (xUnit/vstest results) file corresponds to a passing
test run. Used as a CI gate: exits non-zero whenever:
- the TRX file is missing,
- the <Counters> block reports failed > 0 or error > 0,
- the counters do not sum to the total (sanity check).

Usage: verify_trx.py <path-to-trx-file>
"""
from __future__ import annotations

import sys
import xml.etree.ElementTree as ET
from pathlib import Path

# TRX namespaces — Visual Studio Team Test 2010 and 2012 schemas are
# both seen depending on dotnet test version. We accept either.
NS_CANDIDATES = (
    "{http://microsoft.com/schemas/VisualStudio/TeamTest/2010}",
    "{http://microsoft.com/schemas/VisualStudio/TeamTest/2012}",
)


def find_counters(root: ET.Element) -> ET.Element | None:
    for ns in NS_CANDIDATES:
        node = root.find(f".//{ns}Counters")
        if node is not None:
            return node
    return root.find(".//Counters")


def parse_int(value: str | None) -> int:
    if value is None:
        return 0
    try:
        return int(value)
    except ValueError:
        return 0


def main() -> int:
    if len(sys.argv) != 2:
        print("usage: verify_trx.py <path-to-trx-file>", file=sys.stderr)
        return 2

    trx_path = Path(sys.argv[1])
    if not trx_path.is_file():
        print(f"::error::TRX file not found at {trx_path}")
        return 1

    try:
        tree = ET.parse(trx_path)
    except ET.ParseError as exc:
        print(f"::error::TRX file {trx_path} is not valid XML: {exc}")
        return 1

    root = tree.getroot()
    counters = find_counters(root)
    if counters is None:
        print(f"::error::No <Counters> block found in {trx_path}")
        return 1

    total = parse_int(counters.attrib.get("total"))
    passed = parse_int(counters.attrib.get("passed"))
    failed = parse_int(counters.attrib.get("failed"))
    errored = parse_int(counters.attrib.get("error"))
    skipped_raw = counters.attrib.get("skipped") or counters.attrib.get("skippedAborted")
    skipped = parse_int(skipped_raw if skipped_raw is not None else None)
    aborted = parse_int(counters.attrib.get("aborted"))
    inconclusive = parse_int(counters.attrib.get("inconclusive"))
    not_runnable = parse_int(counters.attrib.get("notRunnable"))
    not_executed = parse_int(counters.attrib.get("notExecuted"))

    if total == 0:
        print(f"::error::TRX reports zero tests executed ({trx_path})")
        return 1

    sum_counted = passed + failed + errored + skipped + aborted + inconclusive + not_runnable + not_executed
    if sum_counted != total:
        print(
            f"::error::TRX counters inconsistent: total={total} but "
            f"passed={passed} failed={failed} error={errored} skipped={skipped} "
            f"aborted={aborted} inconclusive={inconclusive} notRunnable={not_runnable} "
            f"notExecuted={not_executed} sum={sum_counted}"
        )
        return 1

    if failed > 0 or errored > 0:
        print(
            f"::error::{trx_path}: {failed} failed and {errored} errored "
            f"(total={total}, passed={passed})"
        )
        return 1

    print(
        f"OK {trx_path}: total={total} passed={passed} skipped={skipped} "
        f"failed={failed} error={errored} aborted={aborted}"
    )
    return 0


if __name__ == "__main__":
    sys.exit(main())
