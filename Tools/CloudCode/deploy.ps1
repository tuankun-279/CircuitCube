# Deploys the leaderboards backend: builds the Cloud Code module CircuitCubeRanks, packs it as a .ccm and runs
# `ugs deploy` with the boards and access policy in Deploy/. Needs the .NET 6 SDK and the UGS CLI (npm i -g ugs),
# logged in with `ugs login` and set to the project (ugs config set project-id / environment-name production).
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$module = Join-Path $root "CircuitCubeRanks"
$publish = Join-Path $module "bin\publish"
$ccm = Join-Path $root "Build\CircuitCubeRanks.ccm"
if (Test-Path $publish) { Remove-Item -Recurse -Force $publish }
dotnet publish (Join-Path $module "CircuitCubeRanks.csproj") -c Release -r linux-x64 --self-contained false -o $publish
if ($LASTEXITCODE -ne 0) { throw "module build failed" }
New-Item -ItemType Directory -Force (Split-Path $ccm) | Out-Null
if (Test-Path $ccm) { Remove-Item $ccm }
Add-Type -AssemblyName System.IO.Compression.FileSystem
[IO.Compression.ZipFile]::CreateFromDirectory($publish, $ccm)
# UGS refuses a board whose ResetConfig.Start is already in the past, and the whole deploy then exits 1. Such a board
# already exists and keeps resetting on its schedule, so it is left out (a new board always needs a future Start).
$stage = Join-Path $root "Build\Deploy"
if (Test-Path $stage) { Remove-Item -Recurse -Force $stage }
New-Item -ItemType Directory -Force $stage | Out-Null
$now = [DateTime]::UtcNow
foreach ($f in Get-ChildItem (Join-Path $root "Deploy") -File) {
    # the module goes up as $ccm, built above; the .ccmr points at the solution by a path relative to Deploy/
    if ($f.Extension -eq ".ccmr") { continue }
    if ($f.Extension -eq ".lb") {
        # read the raw text: ConvertFrom-Json in PowerShell 7 turns dates into local DateTime values
        $m = [regex]::Match((Get-Content $f.FullName -Raw), '"Start"\s*:\s*"([^"]+)"')
        $start = $m.Groups[1].Value
        if ($m.Success -and [DateTime]::Parse($start, [Globalization.CultureInfo]::InvariantCulture,
                [Globalization.DateTimeStyles]::AdjustToUniversal -bor [Globalization.DateTimeStyles]::AssumeUniversal) -le $now) {
            Write-Host "skip $($f.Name): reset start $start is in the past (board already exists)"
            continue
        }
    }
    Copy-Item $f.FullName $stage
}
ugs deploy $ccm $stage
