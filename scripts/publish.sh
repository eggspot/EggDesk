#!/usr/bin/env bash
# scripts/publish.sh — Build a single-file SpotDesk executable
# Usage:
#   ./scripts/publish.sh                  # auto-detect platform
#   ./scripts/publish.sh win-x64          # explicit RID
#   ./scripts/publish.sh osx-arm64
#   ./scripts/publish.sh linux-x64

set -euo pipefail

ROOT_DIR="$(cd "$(dirname "$0")/.." && pwd)"
PROJECT="$ROOT_DIR/src/SpotDesk.App/SpotDesk.App.csproj"
CONFIG="Release"

# Auto-detect RID if not provided
if [[ $# -ge 1 ]]; then
    RID="$1"
else
    case "$(uname -s)" in
        Darwin)
            ARCH=$(uname -m)
            RID="osx-${ARCH/x86_64/x64}"
            ;;
        Linux)
            RID="linux-x64"
            ;;
        MINGW*|MSYS*|CYGWIN*)
            RID="win-x64"
            ;;
        *)
            echo "Unknown platform — specify RID manually: ./scripts/publish.sh <rid>"
            exit 1
            ;;
    esac
fi

OUTPUT_DIR="$ROOT_DIR/artifacts/$RID"

echo ""
echo "=== Publishing SpotDesk ==="
echo "  RID:    $RID"
echo "  Config: $CONFIG"
echo "  Output: $OUTPUT_DIR"
echo ""

dotnet publish "$PROJECT" \
    -r "$RID" \
    -c "$CONFIG" \
    -o "$OUTPUT_DIR" \
    --self-contained true

echo ""
echo "=== Build complete ==="

# Show output
if [[ -f "$OUTPUT_DIR/SpotDesk" ]]; then
    ls -lh "$OUTPUT_DIR/SpotDesk"
elif [[ -f "$OUTPUT_DIR/SpotDesk.exe" ]]; then
    ls -lh "$OUTPUT_DIR/SpotDesk.exe"
fi

echo ""
echo "Run with:  $OUTPUT_DIR/SpotDesk$([ '$RID' = 'win-x64' ] && echo '.exe' || echo '')"
