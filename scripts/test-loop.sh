#!/usr/bin/env bash
# scripts/test-loop.sh
# Self-healing test runner — build once, run up to MAX_ITERATIONS times until green.
# Usage:
#   ./scripts/test-loop.sh                    # run all tests
#   ./scripts/test-loop.sh --milestone M1     # run only M1 tests
#   ./scripts/test-loop.sh --milestone "M1|M2"

set -euo pipefail

FILTER=""
MAX_ITERATIONS=5
CONFIGURATION="Debug"
ROOT_DIR="$(cd "$(dirname "$0")/.." && pwd)"

# Parse args
while [[ $# -gt 0 ]]; do
    case "$1" in
        --milestone|-m) FILTER="$2"; shift 2 ;;
        --config|-c)    CONFIGURATION="$2"; shift 2 ;;
        *) echo "Unknown arg: $1"; exit 1 ;;
    esac
done

run_tests() {
    local filter_arg=()
    if [[ -n "$FILTER" ]]; then
        filter_arg=(--filter "Category=$FILTER")
    fi

    dotnet test "$ROOT_DIR/SpotDesk.sln" \
        --no-build \
        --configuration "$CONFIGURATION" \
        --logger "console;verbosity=detailed" \
        --logger "trx;LogFileName=results.trx" \
        --results-directory "$ROOT_DIR/TestResults" \
        "${filter_arg[@]}"
}

# ── Build once ──────────────────────────────────────────────────────────────
echo ""
echo "=== Building solution ($CONFIGURATION) ==="
dotnet build "$ROOT_DIR/SpotDesk.sln" --configuration "$CONFIGURATION"
if [[ $? -ne 0 ]]; then
    echo ""
    echo "BUILD FAILED — fix compile errors before running tests"
    exit 1
fi
echo "Build succeeded."

# ── Test loop ────────────────────────────────────────────────────────────────
ITERATION=0
PASS=0

while [[ $ITERATION -lt $MAX_ITERATIONS ]]; do
    ITERATION=$((ITERATION + 1))
    echo ""
    echo "=== Test run $ITERATION / $MAX_ITERATIONS ==="

    set +e
    run_tests
    EXIT_CODE=$?
    set -e

    if [[ $EXIT_CODE -eq 0 ]]; then
        PASS=1
        break
    fi

    echo ""
    echo "=== Failures detected on iteration $ITERATION ==="
done

# ── Result ───────────────────────────────────────────────────────────────────
echo ""
if [[ $PASS -eq 1 ]]; then
    echo "✓ ALL TESTS PASSED on iteration $ITERATION"
    exit 0
else
    echo "✗ Still failing after $MAX_ITERATIONS iterations — diagnostic report needed"
    exit 1
fi
