# Deploys the leaderboards backend: builds the Cloud Code module CircuitCubeRanks, packs it as a .ccm and runs
# `ugs deploy` with the boards and access policy in Deploy/. Needs the .NET 6 SDK and the UGS CLI (npm i -g ugs),
# logged in with `ugs login` and set to the project (ugs config set project-id).
#   .\deploy.ps1 -Env development     the Editor, development, test-lab and sideloaded builds
#   .\deploy.ps1 -Env production      Google Play installs only: deploy here after testing on development
param(
    [Parameter(Mandatory = $true)][ValidateSet("development", "production")][string]$Env
)
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

# UGS refuses a board whose ResetConfig.Start is already in the past, and the whole deploy then exits 1.
# A board that already exists in this environment keeps resetting on its schedule, so it is left out. A board that
# does not exist yet (a new environment) gets its Start moved forward by whole periods of its schedule (a day, or a
# week when the schedule names a weekday), so its resets keep the same time of day and weekday.
$existing = @()
$list = ugs leaderboards list -e $Env -j 2>$null | Out-String
if ($LASTEXITCODE -eq 0 -and $list.Trim().StartsWith("{")) { $existing = @(($list | ConvertFrom-Json).Leaderboards | ForEach-Object { $_.Id }) }
$stage = Join-Path $root "Build\Deploy"
if (Test-Path $stage) { Remove-Item -Recurse -Force $stage }
New-Item -ItemType Directory -Force $stage | Out-Null
$now = [DateTime]::UtcNow
$styles = [Globalization.DateTimeStyles]::AdjustToUniversal -bor [Globalization.DateTimeStyles]::AssumeUniversal
foreach ($f in Get-ChildItem (Join-Path $root "Deploy") -File) {
    # the module goes up as $ccm, built above; the .ccmr points at the solution by a path relative to Deploy/
    if ($f.Extension -eq ".ccmr") { continue }
    if ($f.Extension -eq ".lb") {
        # read the raw text: ConvertFrom-Json in PowerShell 7 turns dates into local DateTime values
        $text = Get-Content $f.FullName -Raw
        $m = [regex]::Match($text, '"Start"\s*:\s*"([^"]+)"')
        if ($m.Success) {
            $start = [DateTime]::Parse($m.Groups[1].Value, [Globalization.CultureInfo]::InvariantCulture, $styles)
            if ($start -le $now) {
                $id = $f.BaseName
                if ($existing -contains $id) { Write-Host "skip $($f.Name): exists in $Env, reset start is in the past"; continue }
                $cron = [regex]::Match($text, '"Schedule"\s*:\s*"([^"]+)"').Groups[1].Value
                $days = if (($cron -split '\s+')[4] -ne '*') { 7 } else { 1 }
                while ($start -le $now.AddMinutes(10)) { $start = $start.AddDays($days) }
                $iso = $start.ToString("yyyy-MM-ddTHH:mm:ssZ", [Globalization.CultureInfo]::InvariantCulture)
                Write-Host "new in ${Env}: $($f.Name) starts $iso"
                $text = $text.Substring(0, $m.Groups[1].Index) + $iso + $text.Substring($m.Groups[1].Index + $m.Groups[1].Length)
                [IO.File]::WriteAllText((Join-Path $stage $f.Name), $text, (New-Object Text.UTF8Encoding($false)))
                continue
            }
        }
    }
    Copy-Item $f.FullName $stage
}
ugs deploy $ccm $stage -e $Env
if ($LASTEXITCODE -ne 0) { throw "ugs deploy failed" }
