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
    "Runtime/Plugins/DEPENDENCIES.sha256"
    "Runtime/Plugins/ThirdPartyNotices.md"
    "Runtime/Plugins/DOTNET-THIRD-PARTY-NOTICES.txt"
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
    if python3 -m json.tool "$UNITY_PKG/package.json" >/dev/null; then
        ok "package.json is valid JSON"
    else
        error "package.json is not valid JSON"
    fi

    # Check required fields
    REQUIRED_FIELDS=("name" "version" "displayName" "description" "unity" "license")

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

    CORE_VERSION=$(sed -n 's:.*<Version>\([^<]*\)</Version>.*:\1:p' "$ROOT_DIR/src/LicenseSeat/LicenseSeat.csproj" | head -1)
    if [ "$PKG_VERSION" = "$CORE_VERSION" ]; then
        ok "Unity package version matches core SDK version '$CORE_VERSION'"
    else
        error "Unity package version '$PKG_VERSION' does not match core SDK version '$CORE_VERSION'"
    fi

    if grep -Fq "## [$PKG_VERSION]" "$UNITY_PKG/CHANGELOG.md"; then
        ok "CHANGELOG.md contains an entry for '$PKG_VERSION'"
    else
        error "CHANGELOG.md has no entry for package version '$PKG_VERSION'"
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

        if python3 -m json.tool "$UNITY_PKG/$asmdef" >/dev/null; then
            ok "$asmdef is valid JSON"
        else
            error "$asmdef is not valid JSON"
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
# 5. Check Managed Dependency Integrity
# ============================================================
echo "5. Checking managed dependency integrity..."

PLUGIN_DIR="$UNITY_PKG/Runtime/Plugins"
PLUGIN_DLLS=(
    "BouncyCastle.Cryptography.dll"
    "Microsoft.Bcl.AsyncInterfaces.dll"
    "System.IO.Pipelines.dll"
    "System.Runtime.CompilerServices.Unsafe.dll"
    "System.Text.Encodings.Web.dll"
    "System.Text.Json.dll"
)

for dll in "${PLUGIN_DLLS[@]}"; do
    if [ -f "$PLUGIN_DIR/$dll" ]; then
        ok "Runtime/Plugins/$dll exists"
    else
        error "Runtime/Plugins/$dll is missing"
    fi

    if [ -f "$PLUGIN_DIR/$dll.meta" ] && grep -q "isExplicitlyReferenced: 1" "$PLUGIN_DIR/$dll.meta"; then
        ok "$dll has an explicit-reference PluginImporter"
    else
        error "$dll.meta is missing or does not disable automatic references"
    fi

    if grep -Fq "\"$dll\"" "$UNITY_PKG/Runtime/LicenseSeat.Unity.Runtime.asmdef"; then
        ok "Runtime assembly explicitly references $dll"
    else
        error "Runtime assembly does not explicitly reference $dll"
    fi
done

DLL_COUNT=$(find "$PLUGIN_DIR" -maxdepth 1 -type f -name '*.dll' | wc -l | tr -d ' ')
if [ "$DLL_COUNT" -eq "${#PLUGIN_DLLS[@]}" ]; then
    ok "No unexpected managed plugin DLLs are present"
else
    error "Expected ${#PLUGIN_DLLS[@]} managed plugin DLLs, found $DLL_COUNT"
fi

if command -v sha256sum >/dev/null 2>&1; then
    if (cd "$PLUGIN_DIR" && sha256sum -c DEPENDENCIES.sha256); then
        ok "Managed dependency hashes match DEPENDENCIES.sha256"
    else
        error "Managed dependency hash verification failed"
    fi
elif command -v shasum >/dev/null 2>&1; then
    if (cd "$PLUGIN_DIR" && shasum -a 256 -c DEPENDENCIES.sha256); then
        ok "Managed dependency hashes match DEPENDENCIES.sha256"
    else
        error "Managed dependency hash verification failed"
    fi
else
    error "Neither sha256sum nor shasum is available for dependency verification"
fi

if grep -q '"overrideReferences"[[:space:]]*:[[:space:]]*true' "$UNITY_PKG/Runtime/LicenseSeat.Unity.Runtime.asmdef"; then
    ok "Runtime assembly overrides implicit plugin references"
else
    error "Runtime assembly must set overrideReferences to true"
fi

echo ""

# ============================================================
# 6. Check Samples Structure
# ============================================================
echo "6. Checking samples structure..."

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
# 7. Check IL2CPP Metadata
# ============================================================
echo "7. Checking IL2CPP metadata..."

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
    ok "IUnityLinkerProcessor implementation found"
else
    error "No IUnityLinkerProcessor implementation found for UPM linker metadata"
    info "Create an Editor script that implements IUnityLinkerProcessor.GenerateAdditionalLinkXmlFile"
fi

echo ""

# ============================================================
# 8. Check Documentation
# ============================================================
echo "8. Checking documentation..."

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
# 9. Check for Common Issues
# ============================================================
echo "9. Checking for common issues..."

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
# 10. OpenUPM Compatibility Check
# ============================================================
echo "10. Checking OpenUPM compatibility..."

# Check package name is lowercase
PKG_NAME_LOWER=$(echo "$PKG_NAME" | tr '[:upper:]' '[:lower:]')
if [ "$PKG_NAME" = "$PKG_NAME_LOWER" ]; then
    ok "Package name is lowercase (OpenUPM requirement)"
else
    error "Package name must be lowercase for OpenUPM"
fi

# Check for license SPDX identifier in package.json (required for OpenUPM)
if grep -q '"license"' "$UNITY_PKG/package.json"; then
    LICENSE_ID=$(grep '"license"' "$UNITY_PKG/package.json" | head -1 | sed 's/.*"license"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/')
    ok "License SPDX identifier: $LICENSE_ID"
else
    error "Missing 'license' field in package.json (SPDX identifier required for OpenUPM)"
fi

# Check for LICENSE file
if [ -f "$UNITY_PKG/LICENSE" ]; then
    ok "LICENSE file exists"
else
    warn "LICENSE file missing (recommended)"
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
    echo -e "${GREEN}All static package checks passed.${NC}"
    echo "Real Unity Editor, player, and IL2CPP build tests are still required before release."
    exit 0
elif [ $ERRORS -eq 0 ]; then
    echo -e "${YELLOW}Package validation completed with $WARNINGS warning(s).${NC}"
    echo "Resolve warnings and run real Unity Editor/player build tests before release."
    exit 0
else
    echo -e "${RED}Package validation failed with $ERRORS error(s) and $WARNINGS warning(s).${NC}"
    echo "Please fix the errors before distribution."
    exit 1
fi
