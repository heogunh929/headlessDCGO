<#
.SYNOPSIS
  Local full test-suite runner (Windows PowerShell).

.DESCRIPTION
  Runs every tests/*.Tests.csproj and reports a PASS/FAIL summary. Use this before committing Phase 4
  card-porting changes — it is the real regression gate (CI only compiles, because the AS-IS parity
  tests need the git-ignored DCGO/ sources that exist locally).

.EXAMPLE
  ./scripts/run-tests.ps1            # run all
  ./scripts/run-tests.ps1 -Filter G3.5
#>
param([string]$Filter = "")

$ErrorActionPreference = "Continue"
Set-Location (Join-Path $PSScriptRoot "..")

# Prefer the locally pinned SDK if present.
if (Test-Path ".dotnet") { $env:PATH = "$((Get-Location).Path)\.dotnet;$env:PATH" }

$pass = 0; $fail = 0; $failed = @()

Get-ChildItem -Path tests -Recurse -Filter *.Tests.csproj | Sort-Object FullName | ForEach-Object {
    $name = $_.BaseName
    if ($Filter -and ($name -notlike "*$Filter*")) { return }

    $out = & dotnet run --project $_.FullName -c Debug -v q --nologo 2>&1
    $code = $LASTEXITCODE

    # Retry on a build-server lock (no test output produced).
    $tries = 0
    while ($code -ne 0 -and ($out -notmatch 'test\(s\)') -and $tries -lt 3) {
        Start-Sleep -Seconds 2
        dotnet build-server shutdown | Out-Null
        $out = & dotnet run --project $_.FullName -c Debug -v q --nologo 2>&1
        $code = $LASTEXITCODE
        $tries++
    }

    if ($code -eq 0) {
        $pass++; Write-Host "PASS $name"
    } else {
        $fail++; $failed += $name
        Write-Host "FAIL $name" -ForegroundColor Red
        ($out | Select-String -Pattern 'FAIL |Exception|expected' | Select-Object -First 3) | ForEach-Object { Write-Host "  $_" }
    }
}

Write-Host ""
Write-Host "===================================="
Write-Host "SUMMARY: PASS=$pass FAIL=$fail TOTAL=$($pass + $fail)"
if ($fail -gt 0) {
    $failed | ForEach-Object { Write-Host "  - $_" }
    exit 1
}
