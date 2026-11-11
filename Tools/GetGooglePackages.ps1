# Downloads the Firebase Unity packages that Packages/manifest.json points at (they are too big for git).
# Run from the project root:  powershell -ExecutionPolicy Bypass -File Tools/GetGooglePackages.ps1
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
$version = '14.1.0'
$dir = Join-Path $PSScriptRoot '..\GooglePackages'
New-Item -ItemType Directory -Force -Path $dir | Out-Null
foreach ($name in 'com.google.firebase.app', 'com.google.firebase.analytics') {
    $file = Join-Path $dir "$name-$version.tgz"
    if (Test-Path $file) { Write-Host "have $name $version"; continue }
    Write-Host "downloading $name $version"
    Invoke-WebRequest -UseBasicParsing -Uri "https://dl.google.com/games/registry/unity/$name/$name-$version.tgz" -OutFile $file
}
