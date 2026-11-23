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
ugs deploy $ccm (Join-Path $root "Deploy")
