Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent $scriptRoot

Set-Location $repoRoot

$publishDir = Join-Path $repoRoot "publish\PicoMoldForge.Generator"
$exePath = Join-Path $publishDir "PicoMoldForge.Generator.exe"
$projectPath = Join-Path $repoRoot "src\PicoMoldForge.Generator\PicoMoldForge.Generator.csproj"
$sampleConfig = Join-Path $repoRoot "samples\generator-valid-project.json"
$sampleStl = Join-Path $repoRoot "samples\generator-sample-binary.stl"
$sampleOutput = Join-Path $repoRoot "samples\generated\generator-sample"

$required = @(
  $projectPath,
  $sampleConfig,
  $sampleStl
)

foreach ($path in $required) {
  if (-not (Test-Path $path)) {
    throw "Missing required generator publish artifact: $path"
  }
}

Write-Host "`n== CLEAN PUBLISH OUTPUT ==" -ForegroundColor Cyan
if (Test-Path $publishDir) {
  Remove-Item $publishDir -Recurse -Force
}

New-Item -ItemType Directory -Force -Path $publishDir | Out-Null

Write-Host "`n== PUBLISH GENERATOR ==" -ForegroundColor Cyan
dotnet publish $projectPath `
  -c Release `
  -r win-x64 `
  --self-contained false `
  -o $publishDir

if (-not (Test-Path $exePath)) {
  throw "Published generator exe was not found: $exePath"
}

Write-Host "`n== RUN PUBLISHED GENERATOR SELF TEST ==" -ForegroundColor Cyan
& $exePath --self-test

if ($LASTEXITCODE -ne 0) {
  throw "Published generator self-test failed."
}

Write-Host "`n== CLEAN SAMPLE OUTPUT ==" -ForegroundColor Cyan
if (Test-Path $sampleOutput) {
  Remove-Item $sampleOutput -Recurse -Force
}

Write-Host "`n== RUN PUBLISHED GENERATOR SAMPLE ==" -ForegroundColor Cyan
& $exePath --config $sampleConfig --generate-all

if ($LASTEXITCODE -ne 0) {
  throw "Published generator sample run failed."
}

Write-Host "`n== VERIFY GENERATED ARTIFACTS ==" -ForegroundColor Cyan

$expected = @(
  "DiagnosticMesh.stl",
  "Cavity.stl",
  "Core.stl",
  "CoolingDiagnostic.stl",
  "LatticeDiagnostic.stl",
  "MoldSystemDiagnostic.stl",
  "FinalProjectReport.json"
)

foreach ($fileName in $expected) {
  $path = Join-Path $sampleOutput $fileName

  if (-not (Test-Path $path)) {
    throw "Expected generated artifact is missing: $path"
  }

  $item = Get-Item $path

  if ($item.Length -le 0) {
    throw "Expected generated artifact is empty: $path"
  }

  Write-Host "OK $path ($($item.Length) bytes)" -ForegroundColor Green
}

Write-Host "`n== VERIFY FINAL REPORT CONTENT ==" -ForegroundColor Cyan

$finalReportPath = Join-Path $sampleOutput "FinalProjectReport.json"
$finalReport = Get-Content $finalReportPath -Raw

if ($finalReport -notmatch '"ProjectName"\s*:\s*"PicoMoldForge Generator Sample"') {
  throw "FinalProjectReport.json does not contain expected project name."
}

if ($finalReport -notmatch '"IsPassing"\s*:\s*true') {
  throw "FinalProjectReport.json does not contain passing baseline status."
}

Write-Host "OK FinalProjectReport.json content verified" -ForegroundColor Green

Write-Host "`n== GENERATOR PUBLISH VERIFY PASS ==" -ForegroundColor Green