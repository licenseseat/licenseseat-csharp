<#
.SYNOPSIS
    Sync Core SDK files to Unity package

.DESCRIPTION
    This script copies the core SDK source files to the Unity package.
    Use this if symlinks don't work in your environment (e.g., Windows without developer mode).

    The Unity package normally uses symlinks to reference the core SDK files,
    which keeps everything in sync automatically. If symlinks don't work,
    run this script after making changes to the core SDK.

.PARAMETER ReplaceSymlinks
    Remove symlinks and copy files (for Windows compatibility)

.PARAMETER RestoreSymlinks
    Remove copied files and restore symlinks (requires admin on Windows)

.PARAMETER Status
    Show current status of Unity Core files

.EXAMPLE
    .\sync-unity-core.ps1 -Status
    .\sync-unity-core.ps1 -ReplaceSymlinks
#>

param(
    [switch]$ReplaceSymlinks,
    [switch]$RestoreSymlinks,
    [switch]$Status
)

$ErrorActionPreference = "Stop"

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$RootDir = Split-Path -Parent $ScriptDir
$CoreSdk = Join-Path $RootDir "src\LicenseSeat"
$UnityCore = Join-Path $RootDir "src\LicenseSeat.Unity\Runtime\Core"

$Files = @(
    "ILicenseSeatClient.cs",
    "LicenseSeat.cs",
    "LicenseSeatClient.cs",
    "LicenseSeatClientOptions.cs"
)

$Dirs = @(
    "Cache",
    "Events",
    "Exceptions",
    "Http",
    "Models",
    "Utilities"
)

function Show-Status {
    Write-Host "Unity Core Status:" -ForegroundColor Cyan
    Write-Host ""

    Push-Location $UnityCore

    foreach ($item in $Files + $Dirs) {
        $path = Join-Path $UnityCore $item
        $attrs = (Get-Item $path -ErrorAction SilentlyContinue).Attributes

        if ($attrs -band [System.IO.FileAttributes]::ReparsePoint) {
            if (Test-Path $path) {
                Write-Host "  [SYMLINK] $item" -ForegroundColor Green
            } else {
                Write-Host "  [BROKEN]  $item" -ForegroundColor Red
            }
        } elseif (Test-Path $path) {
            Write-Host "  [COPY]    $item" -ForegroundColor Yellow
        } else {
            Write-Host "  [MISSING] $item" -ForegroundColor Red
        }
    }

    Pop-Location
}

function Copy-Files {
    Write-Host "Copying files (replacing symlinks)..." -ForegroundColor Cyan

    foreach ($file in $Files) {
        $src = Join-Path $CoreSdk $file
        $dst = Join-Path $UnityCore $file
        Remove-Item -Path $dst -Force -ErrorAction SilentlyContinue
        Copy-Item -Path $src -Destination $dst
        Write-Host "  Copied $file" -ForegroundColor Green
    }

    foreach ($dir in $Dirs) {
        $src = Join-Path $CoreSdk $dir
        $dst = Join-Path $UnityCore $dir
        Remove-Item -Path $dst -Recurse -Force -ErrorAction SilentlyContinue
        New-Item -ItemType Directory -Path $dst -Force | Out-Null
        Get-ChildItem -Path $src -Filter "*.cs" | Copy-Item -Destination $dst
        Write-Host "  Copied $dir/" -ForegroundColor Green
    }

    Write-Host ""
    Write-Host "Files copied successfully." -ForegroundColor Green
    Write-Host "WARNING: Unity Core now has copies. Run with -RestoreSymlinks to go back to symlinks." -ForegroundColor Yellow
}

function Restore-Symlinks {
    Write-Host "Creating symlinks..." -ForegroundColor Cyan
    Write-Host "NOTE: On Windows, this may require administrator privileges or Developer Mode enabled." -ForegroundColor Yellow
    Write-Host ""

    foreach ($file in $Files) {
        $src = "..\..\..\LicenseSeat\$file"
        $dst = Join-Path $UnityCore $file
        Remove-Item -Path $dst -Force -ErrorAction SilentlyContinue
        New-Item -ItemType SymbolicLink -Path $dst -Target $src | Out-Null
        Write-Host "  Linked $file" -ForegroundColor Green
    }

    foreach ($dir in $Dirs) {
        $src = "..\..\..\LicenseSeat\$dir"
        $dst = Join-Path $UnityCore $dir
        Remove-Item -Path $dst -Recurse -Force -ErrorAction SilentlyContinue
        New-Item -ItemType SymbolicLink -Path $dst -Target $src | Out-Null
        Write-Host "  Linked $dir/" -ForegroundColor Green
    }

    Write-Host ""
    Write-Host "Symlinks created successfully." -ForegroundColor Green
}

# Main
if ($ReplaceSymlinks) {
    Copy-Files
} elseif ($RestoreSymlinks) {
    Restore-Symlinks
} elseif ($Status) {
    Show-Status
} else {
    Write-Host "LicenseSeat Unity Core Sync Script" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "Usage: .\sync-unity-core.ps1 [option]"
    Write-Host ""
    Write-Host "Options:"
    Write-Host "  -Status              Show current status of Unity Core files"
    Write-Host "  -ReplaceSymlinks     Replace symlinks with file copies (for Windows)"
    Write-Host "  -RestoreSymlinks     Restore symlinks (requires admin/dev mode)"
    Write-Host ""
    Write-Host "Current status:" -ForegroundColor Cyan
    Write-Host ""
    Show-Status
}
