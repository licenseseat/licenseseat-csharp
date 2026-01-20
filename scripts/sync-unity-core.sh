#!/bin/bash
#
# Sync Core SDK files to Unity package
#
# This script copies the core SDK source files to the Unity package.
# Use this if symlinks don't work in your environment (e.g., Windows without developer mode).
#
# The Unity package normally uses symlinks to reference the core SDK files,
# which keeps everything in sync automatically. If symlinks don't work,
# run this script after making changes to the core SDK.
#
# Usage:
#   ./scripts/sync-unity-core.sh
#
#   Options:
#     --replace-symlinks    Remove symlinks and copy files (for Windows compatibility)
#     --restore-symlinks    Remove copied files and restore symlinks
#

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(dirname "$SCRIPT_DIR")"
CORE_SDK="$ROOT_DIR/src/LicenseSeat"
UNITY_CORE="$ROOT_DIR/src/LicenseSeat.Unity/Runtime/Core"

# Files and directories to sync
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

create_symlinks() {
    echo "Creating symlinks..."
    cd "$UNITY_CORE"

    for file in "${FILES[@]}"; do
        rm -rf "$file"
        ln -s "../../../LicenseSeat/$file" "$file"
        echo "  ✓ $file -> symlink"
    done

    for dir in "${DIRS[@]}"; do
        rm -rf "$dir"
        ln -s "../../../LicenseSeat/$dir" "$dir"
        echo "  ✓ $dir/ -> symlink"
    done

    echo ""
    echo "Symlinks created successfully."
    echo "Changes to core SDK will automatically reflect in Unity package."
}

copy_files() {
    echo "Copying files (replacing symlinks)..."

    for file in "${FILES[@]}"; do
        rm -rf "$UNITY_CORE/$file"
        cp "$CORE_SDK/$file" "$UNITY_CORE/$file"
        echo "  ✓ $file"
    done

    for dir in "${DIRS[@]}"; do
        rm -rf "$UNITY_CORE/$dir"
        mkdir -p "$UNITY_CORE/$dir"
        # Copy .cs files only (skip obj, bin, etc.)
        find "$CORE_SDK/$dir" -maxdepth 1 -name "*.cs" -exec cp {} "$UNITY_CORE/$dir/" \;
        echo "  ✓ $dir/"
    done

    echo ""
    echo "Files copied successfully."
    echo "WARNING: Unity Core now has copies. Run with --restore-symlinks to go back to symlinks."
}

show_status() {
    echo "Unity Core Status:"
    echo ""
    cd "$UNITY_CORE"

    for item in "${FILES[@]}" "${DIRS[@]}"; do
        if [ -L "$item" ]; then
            if [ -e "$item" ]; then
                echo "  ✓ $item (symlink, valid)"
            else
                echo "  ✗ $item (symlink, BROKEN)"
            fi
        elif [ -e "$item" ]; then
            echo "  • $item (copy)"
        else
            echo "  ✗ $item (MISSING)"
        fi
    done
}

case "${1:-}" in
    --replace-symlinks)
        copy_files
        ;;
    --restore-symlinks)
        create_symlinks
        ;;
    --status)
        show_status
        ;;
    *)
        echo "LicenseSeat Unity Core Sync Script"
        echo ""
        echo "Usage: $0 [option]"
        echo ""
        echo "Options:"
        echo "  --status              Show current status of Unity Core files"
        echo "  --replace-symlinks    Replace symlinks with file copies (for Windows)"
        echo "  --restore-symlinks    Restore symlinks (for Unix/macOS)"
        echo ""
        echo "Current status:"
        echo ""
        show_status
        ;;
esac
