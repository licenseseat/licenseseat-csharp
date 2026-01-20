#!/bin/bash
#
# Validate Unity Core files are in sync with Core SDK
#
# This script is run by CI to ensure the Unity package stays in sync
# with the core SDK. It fails if any files are out of sync.
#
# Usage:
#   ./scripts/validate-unity-sync.sh
#

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(dirname "$SCRIPT_DIR")"
CORE_SDK="$ROOT_DIR/src/LicenseSeat"
UNITY_CORE="$ROOT_DIR/src/LicenseSeat.Unity/Runtime/Core"

# Files and directories to check
FILES=(
    "ILicenseSeatClient.cs"
    "LicenseSeat.cs"
    "LicenseSeatClient.cs"
    "LicenseSeatClientOptions.cs"
)

DIRS=(
    "Cache"
    "Events"
    "Exceptions"
    "Http"
    "Models"
    "Utilities"
)

OUT_OF_SYNC=0

echo "Validating Unity Core files are in sync with Core SDK..."
echo ""

# Check individual files
for file in "${FILES[@]}"; do
    core_file="$CORE_SDK/$file"
    unity_file="$UNITY_CORE/$file"

    if [ ! -f "$unity_file" ]; then
        echo "✗ MISSING: $file"
        OUT_OF_SYNC=1
    elif ! diff -q "$core_file" "$unity_file" > /dev/null 2>&1; then
        echo "✗ OUT OF SYNC: $file"
        OUT_OF_SYNC=1
    else
        echo "✓ $file"
    fi
done

# Check directories
for dir in "${DIRS[@]}"; do
    core_dir="$CORE_SDK/$dir"
    unity_dir="$UNITY_CORE/$dir"

    if [ ! -d "$unity_dir" ]; then
        echo "✗ MISSING: $dir/"
        OUT_OF_SYNC=1
        continue
    fi

    # Check each .cs file in the directory
    for core_file in "$core_dir"/*.cs; do
        if [ -f "$core_file" ]; then
            filename=$(basename "$core_file")
            unity_file="$unity_dir/$filename"

            if [ ! -f "$unity_file" ]; then
                echo "✗ MISSING: $dir/$filename"
                OUT_OF_SYNC=1
            elif ! diff -q "$core_file" "$unity_file" > /dev/null 2>&1; then
                echo "✗ OUT OF SYNC: $dir/$filename"
                OUT_OF_SYNC=1
            else
                echo "✓ $dir/$filename"
            fi
        fi
    done
done

echo ""

if [ $OUT_OF_SYNC -eq 1 ]; then
    echo "ERROR: Unity Core files are out of sync with Core SDK!"
    echo ""
    echo "Run the following command to sync:"
    echo "  ./scripts/sync-unity-core.sh --replace-symlinks"
    echo ""
    exit 1
else
    echo "All Unity Core files are in sync with Core SDK."
    exit 0
fi
