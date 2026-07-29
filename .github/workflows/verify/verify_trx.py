#!/usr/bin/env python3
"""
Verify that a TRX (xUnit/vstest results) file corresponds to a passing
test run. Used as a CI gate: exits non-zero whenever:
- the TRX file is missing,
- the file has no <UnitTestResult> entries,
- any individual test has outcome "Failed" or "Error" (or any other
  failure outcome xUnit/vstest may surface — see FAIL_OUTCOMES).

Unlike the previous version that compared <Counters> attribute
sums, this version counts UnitTestResult outcomes directly. xUnit
Skipped tests are reported with outcome "NotExecuted" (or
"NotRunnable") at the UnitTestResult level and the <Counters> element
sometimes does not include them in its "skipped" / "notExecuted"
attributes (vstest bug across versions). Counting outcomes directly
is the only reliable way to derive "executed vs skipped vs failed"
from the TRX.

Usage: verify_trx.py <path-to-trx-file>
"""
from __future__ import annotations

import sys
import xml.etree.ElementTree as ET
from collections import Counter
from pathlib import Path

# TRX namespaces — Visual Studio Team Test 2010 and 2012 schemas are
# both seen depending on dotnet test version. We accept either.
NS_CANDIDATES = (
    "{http://microsoft.com/schemas/VisualStudio/TeamTest/2010}",
    "{http://microsoft.com/schemas/VisualStudio/TeamTest/2012}",
)

# Outcomes that indicate a test run failure. A pass outcome is
# "Passed"; everything else is a problem (or skip / no-op).
FAIL_OUTCOMES = frozenset({
    "Failed",
    "Error",
    "Timeout",
    "Aborted",
    "NotRunnable",   # xUnit may surface a hard skip as this
    "Inconclusive",
    "Warning",       # depends on runner; treat as soft failure
})

# Outcomes that count as "skipped" / "did not run". They must NOT
# fail the gate, but they are NOT a hard pass either.
SKIP_OUTCOMES = frozenset({
    "NotExecuted",
    "Ignored",
    "Inconclusive",
})


def find_unit_test_results(root: ET.Element):
    for ns in NS_CANDIDATES:
        results = root.findall(f".//{ns}UnitTestResult")
        if results:
            return results
    return root.findall(".//UnitTestResult")


def find_counters(root: ET.Element):
    for ns in NS_CANDIDATES:
        counters = root.find(f".//{ns}Counters")
        if counters is not None:
            return counters
    return root.find(".//Counters")


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
    results = find_unit_test_results(root)
    if not results:
        print(f"::error::No <UnitTestResult> entries found in {trx_path}")
        return 1

    outcomes = Counter(r.attrib.get("outcome", "?") for r in results)
    total = sum(outcomes.values())

    if total == 0:
        print(f"::error::TRX reports zero tests executed ({trx_path})")
        return 1

    failed = sum(c for o, c in outcomes.items() if o in FAIL_OUTCOMES)
    if failed > 0:
        # Show a compact summary of non-pass outcomes.
        breakdown = ", ".join(
            f"{o}={c}" for o, c in outcomes.items() if o != "Passed"
        )
        print(
            f"::error::{trx_path}: {failed} non-pass outcomes "
            f"({breakdown}) (total={total}, passed={outcomes.get('Passed', 0)})"
        )
        return 1

    skipped = sum(c for o, c in outcomes.items() if o in SKIP_OUTCOMES)
    print(
        f"OK {trx_path}: total={total} passed={outcomes.get('Passed', 0)} "
        f"skipped={skipped} failed=0"
    )
    return 0


if __name__ == "__main__":
    sys.exit(main())
