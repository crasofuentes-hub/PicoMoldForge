Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$scriptPath = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Resolve-Path (Join-Path $scriptPath "..")

Set-Location $repoRoot

Write-Host "`n== PICOMOLDFORGE FUNCTIONAL MOLD ALPHA VERIFY ==" -ForegroundColor Cyan

$required = @(
  ".\src\PicoMoldForge.Core\Exports\FunctionalMoldAlphaReport.cs",
  ".\src\PicoMoldForge.Core\Engineering\Separation\CoreCavitySeparationValidator.cs",
  ".\src\PicoMoldForge.Core\Engineering\Separation\MoldSeparationEngine.cs",
  ".\src\PicoMoldForge.Core\Engineering\Separation\PartingPlaneScorer.cs",
  ".\src\PicoMoldForge.Core\Engineering\Separation\ShutoffStrategyEvaluator.cs",
  ".\src\PicoMoldForge.Core\Engineering\DraftAnalysis\DraftBasicGeometryAnalyzer.cs",
  ".\src\PicoMoldForge.Core\Engineering\WallThickness\VoxelWallThicknessAnalyzer.cs",
  ".\src\PicoMoldForge.Core\Engineering\Undercuts\UndercutRiskAnalyzer.cs",
  ".\src\PicoMoldForge.Core\Engineering\CoolingGeometry\CoolingChannelSubtractor.cs",
  ".\src\PicoMoldForge.Core\Engineering\GateSystem\GateRunnerSprueGenerator.cs",
  ".\src\PicoMoldForge.Core\Engineering\EjectionGeometry\EjectorCandidateGenerator.cs",
  ".\src\PicoMoldForge.Core\Engineering\Clearance\ClearanceCollisionMatrix.cs",
  ".\tests\PicoMoldForge.Core.Tests\FunctionalMoldAlphaReportTests.cs",
  ".\scripts\verify-baseline.ps1"
)

foreach ($path in $required) {
  if (-not (Test-Path $path)) {
    throw "Missing alpha artifact: $path"
  }

  $item = Get-Item $path
  if ($item.Length -le 0) {
    throw "Alpha artifact is empty: $path"
  }

  Write-Host "OK $path [$($item.Length) bytes]" -ForegroundColor Green
}

Write-Host "`n== CORE TESTS ==" -ForegroundColor Cyan
dotnet test ".\tests\PicoMoldForge.Core.Tests\PicoMoldForge.Core.Tests.csproj"

if ($LASTEXITCODE -ne 0) {
  throw "Core tests failed."
}

Write-Host "`n== FUNCTIONAL MOLD ALPHA TESTS ==" -ForegroundColor Cyan
dotnet test ".\tests\PicoMoldForge.Core.Tests\PicoMoldForge.Core.Tests.csproj" --filter "FullyQualifiedName~FunctionalMoldAlphaReportTests"

if ($LASTEXITCODE -ne 0) {
  throw "Functional Mold Alpha tests failed."
}

Write-Host "`n== BASELINE ==" -ForegroundColor Cyan
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".\scripts\verify-baseline.ps1"

if ($LASTEXITCODE -ne 0) {
  throw "Baseline failed."
}

Write-Host "`n== OPTIONAL GENERATOR SMOKE ==" -ForegroundColor Cyan

$generatorProject = Get-ChildItem ".\src" -Recurse -File -Filter "*.csproj" |
  Where-Object { $_.FullName -match "Generator" } |
  Select-Object -First 1

$sampleConfig = ".\samples\generator-valid-project.json"
$alphaOutput = ".\samples\generated\functional-alpha"

if ($null -ne $generatorProject -and (Test-Path $sampleConfig)) {
  Write-Host "Generator project: $($generatorProject.FullName)" -ForegroundColor Green

  dotnet run --project $generatorProject.FullName -- `
    --config $sampleConfig `
    --generate-all `
    --clean-output `
    --output $alphaOutput

  if ($LASTEXITCODE -ne 0) {
    throw "Generator alpha smoke failed."
  }

  $finalReport = Join-Path $alphaOutput "FinalProjectReport.json"

  if (-not (Test-Path $finalReport)) {
    throw "Generator alpha smoke did not produce FinalProjectReport.json"
  }

  $json = Get-Content $finalReport -Raw

  if ($json -notmatch "FinalProjectReport|ProjectName|Baseline|Warnings") {
    throw "FinalProjectReport.json does not contain expected report fields."
  }

  Write-Host "Generator alpha smoke: PASS" -ForegroundColor Green
}
else {
  Write-Host "Generator smoke skipped: generator project or sample config not found." -ForegroundColor Yellow
}

Write-Host "`nFunctional Mold Alpha verification: PASS" -ForegroundColor Green