#!/bin/bash
#
# Validate Unity Package for UPM Distribution
#
# This script validates that the Unity package is properly structured
# for distribution via Unity Package Manager (UPM) and OpenUPM.
#
# Usage:
#   ./scripts/validate-unity-package.sh
#

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(dirname "$SCRIPT_DIR")"
UNITY_PKG="$ROOT_DIR/src/LicenseSeat.Unity"

ERRORS=0
WARNINGS=0

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

error() {
    echo -e "${RED}✗ ERROR:${NC} $1"
    ERRORS=$((ERRORS + 1))
}

warn() {
    echo -e "${YELLOW}⚠ WARNING:${NC} $1"
    WARNINGS=$((WARNINGS + 1))
}

ok() {
    echo -e "${GREEN}✓${NC} $1"
}

info() {
    echo "  $1"
}

echo "=============================================="
echo "Unity Package Validation for UPM Distribution"
echo "=============================================="
echo ""

# ============================================================
# 1. Check Required Files
# ============================================================
echo "1. Checking required files..."

REQUIRED_FILES=(
    "package.json"
    "README.md"
    "CHANGELOG.md"
    "LICENSE"
    "link.xml"
)

for file in "${REQUIRED_FILES[@]}"; do
    if [ -f "$UNITY_PKG/$file" ]; then
        ok "$file exists"
    else
        error "$file is missing"
    fi
done

echo ""

# ============================================================
# 2. Validate package.json
# ============================================================
echo "2. Validating package.json..."

if [ -f "$UNITY_PKG/package.json" ]; then
    # Check required fields
    REQUIRED_FIELDS=("name" "version" "displayName" "description" "unity")

    for field in "${REQUIRED_FIELDS[@]}"; do
        if grep -q "\"$field\"" "$UNITY_PKG/package.json"; then
            ok "package.json has '$field' field"
        else
            error "package.json missing '$field' field"
        fi
    done

    # Validate package name format (com.company.package)
    PKG_NAME=$(grep '"name"' "$UNITY_PKG/package.json" | head -1 | sed 's/.*"name"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/')
    if echo "$PKG_NAME" | grep -qE '^com\.[a-z0-9]+\.[a-z0-9-]+$'; then
        ok "Package name '$PKG_NAME' follows UPM convention"
    else
        warn "Package name '$PKG_NAME' may not follow UPM convention (com.company.package)"
    fi

    # Validate version format (semver)
    PKG_VERSION=$(grep '"version"' "$UNITY_PKG/package.json" | head -1 | sed 's/.*"version"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/')
    if [[ "$PKG_VERSION" =~ ^[0-9]+\.[0-9]+\.[0-9]+(-[a-zA-Z0-9.]+)?$ ]]; then
        ok "Version '$PKG_VERSION' follows semantic versioning"
    else
        error "Version '$PKG_VERSION' does not follow semantic versioning"
    fi

    # Check Unity version
    UNITY_VERSION=$(grep -o '"unity"[[:space:]]*:[[:space:]]*"[^"]*"' "$UNITY_PKG/package.json" | sed 's/.*"\([^"]*\)"$/\1/')
    ok "Minimum Unity version: $UNITY_VERSION"

    # Check for samples array
    if grep -q '"samples"' "$UNITY_PKG/package.json"; then
        ok "package.json has samples defined"
    else
        warn "package.json has no samples defined"
    fi

    # Check for repository field (needed for OpenUPM)
    if grep -q '"repository"' "$UNITY_PKG/package.json"; then
        ok "package.json has repository field (OpenUPM compatible)"
    else
        warn "package.json missing repository field (recommended for OpenUPM)"
    fi
fi

echo ""

# ============================================================
# 3. Check Directory Structure
# ============================================================
echo "3. Checking directory structure..."

REQUIRED_DIRS=(
    "Runtime"
    "Editor"
)

OPTIONAL_DIRS=(
    "Samples~"
    "Tests"
    "Documentation~"
)

for dir in "${REQUIRED_DIRS[@]}"; do
    if [ -d "$UNITY_PKG/$dir" ]; then
        ok "$dir/ directory exists"
    else
        error "$dir/ directory is missing"
    fi
done

for dir in "${OPTIONAL_DIRS[@]}"; do
    if [ -d "$UNITY_PKG/$dir" ]; then
        ok "$dir/ directory exists"
    else
        warn "$dir/ directory is missing (optional but recommended)"
    fi
done

echo ""

# ============================================================
# 4. Check Assembly Definitions
# ============================================================
echo "4. Checking assembly definitions..."

REQUIRED_ASMDEFS=(
    "Runtime/LicenseSeat.Unity.Runtime.asmdef"
    "Editor/LicenseSeat.Unity.Editor.asmdef"
)

for asmdef in "${REQUIRED_ASMDEFS[@]}"; do
    if [ -f "$UNITY_PKG/$asmdef" ]; then
        ok "$asmdef exists"

        # Validate asmdef has basic JSON structure (opening and closing braces)
        if head -1 "$UNITY_PKG/$asmdef" | grep -q '{' && tail -1 "$UNITY_PKG/$asmdef" | grep -q '}'; then
            ok "$asmdef has valid JSON structure"
        else
            error "$asmdef may not be valid JSON"
        fi
    else
        error "$asmdef is missing"
    fi
done

# Check test assembly definitions
if [ -d "$UNITY_PKG/Tests" ]; then
    if [ -f "$UNITY_PKG/Tests/Runtime/LicenseSeat.Unity.Tests.Runtime.asmdef" ]; then
        ok "Tests/Runtime assembly definition exists"
    else
        warn "Tests/Runtime assembly definition is missing"
    fi

    if [ -f "$UNITY_PKG/Tests/Editor/LicenseSeat.Unity.Tests.Editor.asmdef" ]; then
        ok "Tests/Editor assembly definition exists"
    else
        warn "Tests/Editor assembly definition is missing"
    fi
fi

echo ""

# ============================================================
# 5. Check Samples Structure
# ============================================================
echo "5. Checking samples structure..."

if [ -d "$UNITY_PKG/Samples~" ]; then
    # Count samples
    SAMPLE_COUNT=$(find "$UNITY_PKG/Samples~" -maxdepth 1 -type d | wc -l)
    SAMPLE_COUNT=$((SAMPLE_COUNT - 1))  # Subtract the Samples~ directory itself

    ok "Found $SAMPLE_COUNT sample(s)"

    # Check each sample has a README
    for sample_dir in "$UNITY_PKG/Samples~"/*/; do
        if [ -d "$sample_dir" ]; then
            sample_name=$(basename "$sample_dir")
            if [ -f "$sample_dir/README.md" ]; then
                ok "Sample '$sample_name' has README.md"
            else
                warn "Sample '$sample_name' missing README.md"
            fi

            # Check for assembly definition
            if find "$sample_dir" -name "*.asmdef" | grep -q .; then
                ok "Sample '$sample_name' has assembly definition"
            else
                warn "Sample '$sample_name' missing assembly definition"
            fi
        fi
    done
fi

echo ""

# ============================================================
# 6. Check IL2CPP Support
# ============================================================
echo "6. Checking IL2CPP support..."

if [ -f "$UNITY_PKG/link.xml" ]; then
    ok "link.xml exists"

    # Check for key preservation rules
    if grep -q "LicenseSeat" "$UNITY_PKG/link.xml"; then
        ok "link.xml preserves LicenseSeat types"
    else
        error "link.xml does not preserve LicenseSeat types"
    fi

    if grep -q "System.Text.Json" "$UNITY_PKG/link.xml"; then
        ok "link.xml preserves System.Text.Json types"
    else
        warn "link.xml may need System.Text.Json preservation for IL2CPP"
    fi
fi

# Check for IUnityLinkerProcessor (critical for UPM packages)
if grep -rq "IUnityLinkerProcessor" "$UNITY_PKG/Editor/"*.cs 2>/dev/null; then
    ok "IUnityLinkerProcessor implementation found (link.xml will work in UPM)"
else
    error "No IUnityLinkerProcessor implementation found - link.xml will NOT work in UPM packages!"
    info "Create an Editor script that implements IUnityLinkerProcessor.GenerateAdditionalLinkXmlFile"
fi

echo ""

# ============================================================
# 7. Check Documentation
# ============================================================
echo "7. Checking documentation..."

if [ -d "$UNITY_PKG/Documentation~" ]; then
    DOC_COUNT=$(find "$UNITY_PKG/Documentation~" -name "*.md" | wc -l)
    ok "Found $DOC_COUNT documentation file(s)"

    RECOMMENDED_DOCS=("installation.md" "quickstart.md")
    for doc in "${RECOMMENDED_DOCS[@]}"; do
        if [ -f "$UNITY_PKG/Documentation~/$doc" ]; then
            ok "Documentation~/$doc exists"
        else
            warn "Documentation~/$doc is missing (recommended)"
        fi
    done
fi

echo ""

# ============================================================
# 8. Check for Common Issues
# ============================================================
echo "8. Checking for common issues..."

# Check for .meta files in Samples~ (should not exist)
if find "$UNITY_PKG/Samples~" -name "*.meta" 2>/dev/null | grep -q .; then
    warn "Found .meta files in Samples~/ - these may cause issues"
else
    ok "No .meta files in Samples~/"
fi

# Check for .meta files in Documentation~ (should not exist)
if find "$UNITY_PKG/Documentation~" -name "*.meta" 2>/dev/null | grep -q .; then
    warn "Found .meta files in Documentation~/ - these may cause issues"
else
    ok "No .meta files in Documentation~/"
fi

# Check README mentions Git URL installation
if grep -q "git.*path=\|?path=" "$UNITY_PKG/README.md" 2>/dev/null; then
    ok "README.md documents Git URL with path parameter"
else
    warn "README.md should document Git URL installation with ?path= parameter"
fi

echo ""

# ============================================================
# 9. OpenUPM Compatibility Check
# ============================================================
echo "9. Checking OpenUPM compatibility..."

# Check package name is lowercase
PKG_NAME_LOWER=$(echo "$PKG_NAME" | tr '[:upper:]' '[:lower:]')
if [ "$PKG_NAME" = "$PKG_NAME_LOWER" ]; then
    ok "Package name is lowercase (OpenUPM requirement)"
else
    error "Package name must be lowercase for OpenUPM"
fi

# Check for license field or file
if grep -q '"license"' "$UNITY_PKG/package.json" || [ -f "$UNITY_PKG/LICENSE" ]; then
    ok "License is specified"
else
    error "License must be specified for OpenUPM"
fi

echo ""

# ============================================================
# Summary
# ============================================================
echo "=============================================="
echo "Validation Summary"
echo "=============================================="
echo ""

if [ $ERRORS -eq 0 ] && [ $WARNINGS -eq 0 ]; then
    echo -e "${GREEN}All checks passed! Package is ready for UPM distribution.${NC}"
    exit 0
elif [ $ERRORS -eq 0 ]; then
    echo -e "${YELLOW}Package validation completed with $WARNINGS warning(s).${NC}"
    echo "The package should work, but consider addressing the warnings."
    exit 0
else
    echo -e "${RED}Package validation failed with $ERRORS error(s) and $WARNINGS warning(s).${NC}"
    echo "Please fix the errors before distribution."
    exit 1
fi
