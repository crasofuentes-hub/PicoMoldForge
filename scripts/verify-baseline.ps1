Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

dotnet restore
dotnet build
dotnet test
dotnet run --project src/PicoMoldForge.Cli -- --self-test