Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
Set-Location $repoRoot

$publishOutput = Join-Path $repoRoot "publish\PicoMoldForge.Generator"
$exePath = Join-Path $publishOutput "PicoMoldForge.Generator.exe"
$projectPath = Join-Path $repoRoot "src\PicoMoldForge.Generator\PicoMoldForge.Generator.csproj"
$sampleConfig = Join-Path $repoRoot "samples\generator-valid-project.json"
$sampleOutput = Join-Path $repoRoot "samples\generated\generator-sample"

Write-Host "`n== CLEAN PUBLISH OUTPUT ==" -ForegroundColor Cyan

if (Test-Path $publishOutput) {
  Remove-Item -Recurse -Force $publishOutput
}

Write-Host "`n== PUBLISH GENERATOR ==" -ForegroundColor Cyan

dotnet publish $projectPath `
  -c Release `
  -r win-x64 `
  --self-contained false `
  -o $publishOutput

if (-not (Test-Path $exePath)) {
  throw "Published generator exe was not found: $exePath"
}

Write-Host "`n== GENERATOR SELF TEST ==" -ForegroundColor Cyan

& $exePath --self-test

if ($LASTEXITCODE -ne 0) {
  throw "Generator self-test failed."
}

Write-Host "`n== GENERATOR HELP ==" -ForegroundColor Cyan

$helpOutput = & $exePath --help

if ($LASTEXITCODE -ne 0) {
  throw "Generator help command failed."
}

$helpOutput | Out-Host

$requiredHelpTerms = @(
  "--config <path>",
  "--generate-all",
  "--clean-output",
  "--output <path>",
  "RunManifest.json"
)

foreach ($term in $requiredHelpTerms) {
  if (($helpOutput -join "`n") -notmatch [regex]::Escape($term)) {
    throw "Generator help output is missing required term: $term"
  }
}

Write-Host "`n== GENERATOR SAMPLE RUN ==" -ForegroundColor Cyan

& $exePath `
  --config $sampleConfig `
  --generate-all `
  --clean-output `
  --output $sampleOutput

if ($LASTEXITCODE -ne 0) {
  throw "Generator sample run failed."
}

Write-Host "`n== VERIFY GENERATED ARTIFACTS ==" -ForegroundColor Cyan

$expectedArtifacts = @(
  "DiagnosticMesh.stl",
  "Cavity.stl",
  "BooleanCavity.stl",
  "Core.stl",
  "BooleanCoreSide.stl",
  "BooleanCavitySide.stl",
  "CoolingDiagnostic.stl",
  "LatticeDiagnostic.stl",
  "MoldSystemDiagnostic.stl",
  "FinalProjectReport.json",
  "RunManifest.json"
)

foreach ($artifactName in $expectedArtifacts) {
  $artifactPath = Join-Path $sampleOutput $artifactName

  if (-not (Test-Path $artifactPath)) {
    throw "Expected generated artifact was not found: $artifactPath"
  }

  $artifactInfo = Get-Item $artifactPath

  if ($artifactInfo.Length -le 0) {
    throw "Generated artifact is empty: $artifactPath"
  }

  Write-Host "OK $artifactName $($artifactInfo.Length) bytes" -ForegroundColor Green
}

Write-Host "`n== VERIFY FINAL REPORT CONTENT ==" -ForegroundColor Cyan

$finalReportPath = Join-Path $sampleOutput "FinalProjectReport.json"
$finalReport = Get-Content $finalReportPath -Raw | ConvertFrom-Json

if ([string]::IsNullOrWhiteSpace($finalReport.ProjectName)) {
  throw "FinalProjectReport.json is missing ProjectName."
}

Write-Host "FinalProjectReport ProjectName: $($finalReport.ProjectName)" -ForegroundColor Green

Write-Host "`n== VERIFY RUN MANIFEST CONTENT ==" -ForegroundColor Cyan

$runManifestPath = Join-Path $sampleOutput "RunManifest.json"
$runManifest = Get-Content $runManifestPath -Raw | ConvertFrom-Json

if ($runManifest.SchemaVersion -ne "picomoldforge.run-manifest.v1") {
  throw "RunManifest.json has unexpected SchemaVersion: $($runManifest.SchemaVersion)"
}

if ($runManifest.CleanOutput -ne $true) {
  throw "RunManifest.json did not record CleanOutput=true."
}

if ($runManifest.UsedOutputOverride -ne $true) {
  throw "RunManifest.json did not record UsedOutputOverride=true."
}

if ([string]::IsNullOrWhiteSpace($runManifest.OutputOverridePath)) {
  throw "RunManifest.json is missing OutputOverridePath."
}

if ($runManifest.Artifacts.Count -lt 10) {
  throw "RunManifest.json recorded fewer artifacts than expected."
}

foreach ($artifact in $runManifest.Artifacts) {
  if ([string]::IsNullOrWhiteSpace($artifact.FileName)) {
    throw "RunManifest artifact is missing FileName."
  }

  if ([string]::IsNullOrWhiteSpace($artifact.Path)) {
    throw "RunManifest artifact is missing Path."
  }

  if ($artifact.SizeBytes -le 0) {
    throw "RunManifest artifact has invalid SizeBytes: $($artifact.FileName)"
  }

  if ([string]::IsNullOrWhiteSpace($artifact.Sha256)) {
    throw "RunManifest artifact is missing Sha256: $($artifact.FileName)"
  }

  if ($artifact.Sha256 -notmatch "^[a-f0-9]{64}$") {
    throw "RunManifest artifact has invalid Sha256 format: $($artifact.FileName)"
  }

  $computedHash = (Get-FileHash -Path $artifact.Path -Algorithm SHA256).Hash.ToLowerInvariant()

  if ($computedHash -ne $artifact.Sha256) {
    throw "RunManifest artifact Sha256 mismatch: $($artifact.FileName)"
  }
}

Write-Host "RunManifest artifacts: $($runManifest.Artifacts.Count)" -ForegroundColor Green
Write-Host "RunManifest output: $($runManifest.OutputDirectory)" -ForegroundColor Green
Write-Host "RunManifest SHA256 checks: PASS" -ForegroundColor Green

Write-Host "`n== GENERATOR PUBLISH VERIFY PASS ==" -ForegroundColor Green