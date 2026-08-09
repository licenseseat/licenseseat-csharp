#!/usr/bin/env python3
"""Validate the exact, security-relevant shape of a LicenseSeat NuGet package."""

from __future__ import annotations

import argparse
from pathlib import Path, PurePosixPath
import re
import sys
import xml.etree.ElementTree as ET
import zipfile


EXPECTED_DEPENDENCIES = {
    "BouncyCastle.Cryptography": "2.6.2",
    "Microsoft.Extensions.DependencyInjection.Abstractions": "10.0.10",
    "Microsoft.Extensions.Options": "10.0.10",
    "System.Text.Json": "10.0.10",
}

EXPECTED_FILES = {
    "_rels/.rels",
    "LicenseSeat.nuspec",
    "lib/netstandard2.0/LicenseSeat.dll",
    "lib/netstandard2.0/LicenseSeat.xml",
    "README.md",
    "[Content_Types].xml",
}

FORBIDDEN_SUFFIXES = {
    ".config",
    ".env",
    ".key",
    ".pem",
    ".pfx",
    ".p12",
    ".snk",
}


def local_name(tag: str) -> str:
    return tag.rsplit("}", 1)[-1]


def child(parent: ET.Element, name: str) -> ET.Element:
    for element in parent:
        if local_name(element.tag) == name:
            return element
    raise ValueError(f"nuspec is missing <{name}>")


def text_of(parent: ET.Element, name: str) -> str:
    value = child(parent, name).text
    if value is None or not value.strip():
        raise ValueError(f"nuspec <{name}> is empty")
    return value.strip()


def validate_entry_name(name: str) -> None:
    path = PurePosixPath(name)
    if not name or "\\" in name or name.startswith("/") or any(part in ("", ".", "..") for part in path.parts):
        raise ValueError(f"unsafe archive entry: {name!r}")
    if path.suffix.lower() in FORBIDDEN_SUFFIXES:
        raise ValueError(f"forbidden sensitive file in package: {name}")


def validate_package(package: Path, expected_version: str) -> None:
    if package.suffix != ".nupkg" or package.name.endswith(".snupkg"):
        raise ValueError("input must be the primary .nupkg, not a symbol package")
    if package.stat().st_size > 2 * 1024 * 1024:
        raise ValueError("package exceeds the 2 MiB safety limit")

    with zipfile.ZipFile(package) as archive:
        bad_member = archive.testzip()
        if bad_member is not None:
            raise ValueError(f"archive CRC validation failed for {bad_member}")

        entries = archive.infolist()
        names = [entry.filename for entry in entries if not entry.is_dir()]
        if len(names) != len(set(names)):
            raise ValueError("archive contains duplicate entry names")
        if sum(entry.file_size for entry in entries) > 8 * 1024 * 1024:
            raise ValueError("uncompressed package exceeds the 8 MiB safety limit")
        for name in names:
            validate_entry_name(name)

        core_properties = [
            name
            for name in names
            if re.fullmatch(r"package/services/metadata/core-properties/[0-9a-f]{32}\.psmdcp", name)
        ]
        expected = EXPECTED_FILES | set(core_properties)
        if len(core_properties) != 1 or set(names) != expected:
            missing = sorted(expected - set(names))
            unexpected = sorted(set(names) - expected)
            raise ValueError(f"unexpected package contents; missing={missing}, unexpected={unexpected}")

        try:
            nuspec_bytes = archive.read("LicenseSeat.nuspec")
        except KeyError as error:
            raise ValueError("LicenseSeat.nuspec is missing") from error
        if len(nuspec_bytes) > 256 * 1024:
            raise ValueError("nuspec exceeds the size limit")

    try:
        root = ET.fromstring(nuspec_bytes)
    except ET.ParseError as error:
        raise ValueError(f"nuspec XML is invalid: {error}") from error
    metadata = child(root, "metadata")

    if text_of(metadata, "id") != "LicenseSeat":
        raise ValueError("unexpected NuGet package ID")
    if text_of(metadata, "version") != expected_version:
        raise ValueError("NuGet version does not match the requested release version")
    if text_of(metadata, "license") != "MIT":
        raise ValueError("NuGet package license is not MIT")
    if text_of(metadata, "readme") != "README.md":
        raise ValueError("NuGet readme metadata is invalid")
    if text_of(metadata, "projectUrl") != "https://github.com/licenseseat/licenseseat-csharp":
        raise ValueError("NuGet project URL is invalid")

    repository = child(metadata, "repository")
    if repository.attrib.get("type") != "git" or repository.attrib.get("url") != "https://github.com/licenseseat/licenseseat-csharp":
        raise ValueError("NuGet repository provenance metadata is invalid")
    commit = repository.attrib.get("commit", "")
    if not re.fullmatch(r"[0-9a-f]{40}", commit):
        raise ValueError("NuGet repository commit is missing or invalid")

    dependencies = child(metadata, "dependencies")
    groups = [element for element in dependencies if local_name(element.tag) == "group"]
    if len(groups) != 1 or groups[0].attrib.get("targetFramework") != ".NETStandard2.0":
        raise ValueError("NuGet target framework dependency group is invalid")
    actual_dependencies = {
        element.attrib.get("id", ""): element.attrib.get("version", "")
        for element in groups[0]
        if local_name(element.tag) == "dependency"
    }
    if actual_dependencies != EXPECTED_DEPENDENCIES:
        raise ValueError(f"unexpected dependency graph: {actual_dependencies}")


def validate_symbols(package: Path, expected_version: str) -> None:
    symbols = package.with_name(f"LicenseSeat.{expected_version}.snupkg")
    if not symbols.is_file():
        raise ValueError(f"symbol package is missing: {symbols.name}")
    if symbols.stat().st_size > 2 * 1024 * 1024:
        raise ValueError("symbol package exceeds the 2 MiB safety limit")

    with zipfile.ZipFile(symbols) as archive:
        if archive.testzip() is not None:
            raise ValueError("symbol package CRC validation failed")
        names = [entry.filename for entry in archive.infolist() if not entry.is_dir()]
        for name in names:
            validate_entry_name(name)
        pdbs = [name for name in names if name == "lib/netstandard2.0/LicenseSeat.pdb"]
        if len(pdbs) != 1:
            raise ValueError("symbol package does not contain the expected portable PDB")


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("package", type=Path)
    parser.add_argument("--version", required=True)
    args = parser.parse_args()

    if not re.fullmatch(r"[0-9]+\.[0-9]+\.[0-9]+(?:-[0-9A-Za-z.-]+)?", args.version):
        parser.error("--version must be a semantic version")
    if not args.package.is_file():
        parser.error(f"package does not exist: {args.package}")

    try:
        validate_package(args.package, args.version)
        validate_symbols(args.package, args.version)
    except (OSError, ValueError, zipfile.BadZipFile) as error:
        print(f"NuGet package validation failed: {error}", file=sys.stderr)
        return 1

    print(f"Validated {args.package.name} and its symbol package.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
