$ErrorActionPreference = "Stop"

$root = Resolve-Path (Join-Path $PSScriptRoot "..")
$packageJsonPath = Join-Path $root "package.json"
$package = Get-Content -Path $packageJsonPath -Raw | ConvertFrom-Json
$version = $package.version

$releaseExe = Join-Path $root "src-tauri\target\release\blue-battery.exe"
$releaseRoot = Join-Path $root "release"
$packageName = "BlueBattery-demo-v$version"
$stagingDir = Join-Path $releaseRoot $packageName
$zipPath = Join-Path $releaseRoot "$packageName.zip"

npm run tauri:build:release

if (-not (Test-Path -LiteralPath $releaseExe)) {
    throw "Release executable was not produced: $releaseExe"
}

New-Item -ItemType Directory -Force -Path $releaseRoot | Out-Null
if (Test-Path -LiteralPath $stagingDir) {
    Remove-Item -LiteralPath $stagingDir -Recurse -Force
}
if (Test-Path -LiteralPath $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}

New-Item -ItemType Directory -Force -Path $stagingDir | Out-Null
Copy-Item -LiteralPath $releaseExe -Destination (Join-Path $stagingDir "blue-battery.exe")

$readmePath = Join-Path $root "README.md"
if (Test-Path -LiteralPath $readmePath) {
    Copy-Item -LiteralPath $readmePath -Destination (Join-Path $stagingDir "README.md")
}

@"
Blue Battery demo v$version

Run blue-battery.exe. The app starts in the Windows notification area.

This demo only shows currently connected Bluetooth LE devices whose battery
level is readable through Windows standard Battery Service APIs.
"@ | Set-Content -Path (Join-Path $stagingDir "DEMO_NOTES.txt") -Encoding UTF8

Compress-Archive -Path (Join-Path $stagingDir "*") -DestinationPath $zipPath
Write-Host "Created demo package: $zipPath"
