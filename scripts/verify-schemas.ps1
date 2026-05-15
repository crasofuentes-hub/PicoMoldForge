Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
Set-Location $repoRoot

Write-Host "`n== VERIFY JSON SCHEMA FILES PARSE ==" -ForegroundColor Cyan

$schemaFiles = @(
  "docs\schemas\picomoldforge.project-config.schema.json",
  "docs\schemas\picomoldforge.final-project-report.schema.json",
  "docs\schemas\picomoldforge.run-manifest.schema.json"
)

foreach ($schemaFile in $schemaFiles) {
  $schemaPath = Join-Path $repoRoot $schemaFile

  if (-not (Test-Path $schemaPath)) {
    throw "Missing schema file: $schemaPath"
  }

  $schema = Get-Content $schemaPath -Raw | ConvertFrom-Json

  if ([string]::IsNullOrWhiteSpace($schema.'$schema')) {
    throw "Schema file is missing `$schema: $schemaFile"
  }

  if ([string]::IsNullOrWhiteSpace($schema.title)) {
    throw "Schema file is missing title: $schemaFile"
  }

  Write-Host "OK $schemaFile" -ForegroundColor Green
}

Write-Host "`n== VERIFY SAMPLE CONFIG STRUCTURE ==" -ForegroundColor Cyan

$sampleConfigPath = Join-Path $repoRoot "samples\generator-valid-project.json"

if (-not (Test-Path $sampleConfigPath)) {
  throw "Missing sample config: $sampleConfigPath"
}

$config = Get-Content $sampleConfigPath -Raw | ConvertFrom-Json

$requiredConfigProperties = @(
  "projectName",
  "inputPath",
  "outputDirectory",
  "mode",
  "standard",
  "voxelResolutionMm",
  "material",
  "machine",
  "moldBlock",
  "cooling",
  "lattice",
  "moldSystem",
  "dfam"
)

foreach ($propertyName in $requiredConfigProperties) {
  if (-not ($config.PSObject.Properties.Name -contains $propertyName)) {
    throw "Sample config is missing required property: $propertyName"
  }
}

Write-Host "Sample project config structure: PASS" -ForegroundColor Green

Write-Host "`n== VERIFY RUN MANIFEST SCHEMA CONTRACT ==" -ForegroundColor Cyan

$runManifestSchemaPath = Join-Path $repoRoot "docs\schemas\picomoldforge.run-manifest.schema.json"
$runManifestSchemaText = Get-Content $runManifestSchemaPath -Raw

$requiredManifestTerms = @(
  "SchemaVersion",
  "GeneratedAtUtc",
  "Artifacts",
  "FileName",
  "Path",
  "SizeBytes",
  "Sha256",
  "^[a-f0-9]{64}$"
)

foreach ($term in $requiredManifestTerms) {
  if ($runManifestSchemaText -notmatch [regex]::Escape($term)) {
    throw "RunManifest schema is missing required term: $term"
  }
}

Write-Host "RunManifest schema contract: PASS" -ForegroundColor Green

Write-Host "`n== VERIFY SCHEMAS PASS ==" -ForegroundColor Green