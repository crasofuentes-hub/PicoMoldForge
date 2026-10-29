# PicoMoldForge Parts - demo launcher for Windows PowerShell 5.1.
#
# Builds the Parts projects (Release), runs their tests (including the PicoGK native tests),
# generates the five sample parts with the reference mesher plus the PicoGK cross-check,
# zips outputs and logs to the Desktop and opens the "modo malla" viewer in the browser.
#
# Usage (from the repository root):
#   powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\run-parts-demo.ps1
# Options:
#   -SkipTests      do not run the Parts test projects
#   -FullBaseline   also run every test project of the solution (existing mold tests included)
#   -NoViewer       do not start the viewer at the end
#   -Port <n>       viewer port (default 8765)
#
# Exit code: 0 when every step passed, 1 otherwise. The viewer runs in its own window.
# Out of scope by design: tool paths / G-code (CAM) and STEP export.

param(
  [switch]$SkipTests,
  [switch]$FullBaseline,
  [switch]$NoViewer,
  [int]$Port = 8765
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot "..")).Path
Set-Location -LiteralPath $repoRoot

$stamp = Get-Date -Format "yyyyMMdd-HHmmss"
$logDir = Join-Path $repoRoot (Join-Path "parts-logs" $stamp)
$outputDir = Join-Path $repoRoot "parts-output"
$cliDll = Join-Path $repoRoot "src\PicoMoldForge.Parts.Cli\bin\Release\net9.0\PicoMoldForge.Parts.Cli.dll"
$results = New-Object System.Collections.Generic.List[object]
$notes = New-Object System.Collections.Generic.List[string]

New-Item -ItemType Directory -Force -Path $logDir | Out-Null

function Add-Result {
  param([string]$Step, [string]$Status, [string]$Detail)
  $results.Add([pscustomobject]@{ Paso = $Step; Estado = $Status; Detalle = $Detail }) | Out-Null
}

function Invoke-Logged {
  param([string]$Step, [string]$CommandLine, [string]$LogName)
  $logFile = Join-Path $logDir $LogName
  Write-Host ""
  Write-Host ("== " + $Step) -ForegroundColor Cyan
  Write-Host ("   " + $CommandLine)
  $full = $CommandLine + ' > "' + $logFile + '" 2>&1'
  cmd /c $full
  $code = $LASTEXITCODE
  if ($code -eq 0) {
    Write-Host ("   OK  (log: " + $logFile + ")") -ForegroundColor Green
  } else {
    Write-Host ("   FALLO con codigo " + $code + "  (log: " + $logFile + ")") -ForegroundColor Red
    Get-Content -LiteralPath $logFile -Tail 25 | ForEach-Object { Write-Host ("   | " + $_) }
  }
  return $code
}

# The launcher's own record (commit, closed processes, step table, verdict) goes into the logs folder,
# so the ZIP carries the same verdict the console shows. The ZIP step itself runs after it.
function Save-LauncherSummary {
  try {
    $lines = New-Object System.Collections.Generic.List[string]
    $lines.Add("PicoMoldForge Parts - resumen del lanzador")
    $lines.Add("Fecha:       " + (Get-Date -Format "yyyy-MM-dd HH:mm:ss zzz"))
    $lines.Add("Repositorio: " + $repoRoot)
    $commit = @(cmd /c ('git -C "' + $repoRoot + '" rev-parse HEAD 2>nul'))
    if ($commit.Count -gt 0) { $lines.Add("Commit:      " + $commit[0]) } else { $lines.Add("Commit:      (no disponible)") }
    foreach ($note in $notes) { $lines.Add($note) }
    $lines.Add("")
    $lines.Add(($results | Format-Table -AutoSize | Out-String -Width 220).TrimEnd())
    $failedSteps = @($results | Where-Object { $_.Estado -eq "FAIL" }).Count
    if ($failedSteps -eq 0) { $lines.Add("Veredicto: PASS") } else { $lines.Add("Veredicto: FAIL (" + $failedSteps + " paso(s) con falla)") }
    [System.IO.File]::WriteAllLines((Join-Path $logDir "launcher-summary.txt"), $lines)
  } catch {
    Write-Host ("No se pudo escribir launcher-summary.txt: " + $_.Exception.Message) -ForegroundColor Yellow
  }
}

# One zip on the Desktop with the logs and the outputs. Entry names use "/" so the zip also opens
# correctly outside Windows (Compress-Archive in Windows PowerShell 5.1 writes "\").
function Save-Zip {
  try {
    Add-Type -AssemblyName System.IO.Compression
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $desktop = [Environment]::GetFolderPath("Desktop")
    $zipPath = Join-Path $desktop ("PicoMoldForge-Parts-" + $stamp + ".zip")
    if (Test-Path -LiteralPath $zipPath) { Remove-Item -LiteralPath $zipPath -Force }
    $folders = @($logDir)
    if (Test-Path -LiteralPath $outputDir) { $folders += $outputDir }
    $archive = [System.IO.Compression.ZipFile]::Open($zipPath, [System.IO.Compression.ZipArchiveMode]::Create)
    try {
      foreach ($folder in $folders) {
        foreach ($file in (Get-ChildItem -LiteralPath $folder -Recurse -File)) {
          $entryName = $file.FullName.Substring($repoRoot.Length).TrimStart('\').Replace('\', '/')
          [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $file.FullName, $entryName, [System.IO.Compression.CompressionLevel]::Optimal) | Out-Null
        }
      }
    } finally {
      $archive.Dispose()
    }
    Add-Result "ZIP en el Escritorio" "PASS" $zipPath
    cmd /c ('explorer /select,"' + $zipPath + '"') | Out-Null
  } catch {
    Add-Result "ZIP en el Escritorio" "FAIL" $_.Exception.Message
  }
}

function Show-Summary {
  Write-Host ""
  Write-Host "== RESUMEN" -ForegroundColor Cyan
  $results | Format-Table -AutoSize | Out-String | Write-Host
  $failedSteps = @($results | Where-Object { $_.Estado -eq "FAIL" }).Count
  if ($failedSteps -eq 0) {
    Write-Host "PICOMOLDFORGE PARTS: PASS" -ForegroundColor Green
  } else {
    Write-Host ("PICOMOLDFORGE PARTS: FAIL (" + $failedSteps + " paso(s) con falla)") -ForegroundColor Red
  }
  return $failedSteps
}

Write-Host "PicoMoldForge Parts - demo" -ForegroundColor Cyan
Write-Host ("Repositorio: " + $repoRoot)
Write-Host ("Logs:        " + $logDir)

# 1. Tooling
$dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
if ($null -eq $dotnet) {
  Write-Host "No se encontro 'dotnet'. Instala el .NET SDK 9 (https://dotnet.microsoft.com/download) y vuelve a correr este script." -ForegroundColor Red
  exit 1
}

$sdkList = cmd /c "dotnet --list-sdks"
$hasSdk9 = $false
foreach ($line in $sdkList) {
  if ($line -match '^9\.') { $hasSdk9 = $true }
}
if (-not $hasSdk9) {
  Write-Host "Se requiere el .NET SDK 9 (PicoGK 2.0 usa net9.0). SDKs instalados:" -ForegroundColor Red
  $sdkList | ForEach-Object { Write-Host ("   " + $_) }
  exit 1
}
Add-Result "Herramientas" "PASS" ("dotnet en " + $dotnet.Source)

# 2. A viewer left open by an earlier run (or any other instance of the program) keeps its files
#    open and the build cannot replace them. Close every running PicoMoldForge.Parts.Cli first.
$running = @()
try {
  $running = @(Get-CimInstance -ClassName Win32_Process -Filter "Name = 'dotnet.exe'" -ErrorAction Stop |
    Where-Object { $_.CommandLine -and ($_.CommandLine.IndexOf("PicoMoldForge.Parts.Cli.dll", [System.StringComparison]::OrdinalIgnoreCase) -ge 0) })
} catch {
  Write-Host ("No se pudo revisar si hay un visor abierto: " + $_.Exception.Message) -ForegroundColor Yellow
  $notes.Add("No se pudo revisar si habia un visor abierto: " + $_.Exception.Message)
}
foreach ($process in $running) {
  Write-Host ("Cerrando una ejecucion anterior del programa (PID " + $process.ProcessId + "), por ejemplo un visor que quedo abierto.") -ForegroundColor Yellow
  $notes.Add("Cerrada una ejecucion anterior del programa: PID " + $process.ProcessId + " | " + $process.CommandLine)
  Stop-Process -Id $process.ProcessId -Force -ErrorAction SilentlyContinue
}
if ($running.Count -eq 0) { $notes.Add("No habia ejecuciones anteriores del programa abiertas.") }
if ($running.Count -gt 0) {
  Start-Sleep -Seconds 2
  Add-Result "Ejecuciones anteriores" "PASS" ($running.Count.ToString() + " cerrada(s)")
}

# 3. Build (Release). The test projects reference the Core, PicoGK adapter and CLI projects.
$buildFailed = $false
$projects = @(
  "tests\PicoMoldForge.Parts.Core.Tests\PicoMoldForge.Parts.Core.Tests.csproj",
  "tests\PicoMoldForge.Parts.PicoGK.Tests\PicoMoldForge.Parts.PicoGK.Tests.csproj",
  "tests\PicoMoldForge.Parts.Cli.Tests\PicoMoldForge.Parts.Cli.Tests.csproj"
)
$index = 0
$lockedFile = $false
foreach ($project in $projects) {
  $index++
  $logName = "build-" + $index + ".log"
  $code = Invoke-Logged ("Compilar " + [System.IO.Path]::GetFileNameWithoutExtension($project)) ('dotnet build "' + (Join-Path $repoRoot $project) + '" -c Release') $logName
  if ($code -ne 0) {
    $buildFailed = $true
    if (Select-String -LiteralPath (Join-Path $logDir $logName) -Pattern "MSB3027|MSB3021|being used by another process" -Quiet) { $lockedFile = $true }
  }
}
if ($buildFailed) {
  if ($lockedFile) {
    Add-Result "Compilacion" "FAIL" "otro programa tiene abierto un archivo de la compilacion (el log dice cual): cierralo y vuelve a correr"
  } else {
    Add-Result "Compilacion" "FAIL" "ver logs build-*.log"
  }
} else {
  Add-Result "Compilacion" "PASS" "Release"
}

if ($buildFailed -or -not (Test-Path -LiteralPath $cliDll)) {
  Write-Host ""
  Write-Host "La compilacion fallo; no se puede continuar. Los logs quedan en el ZIP del Escritorio." -ForegroundColor Red
  Save-LauncherSummary
  Save-Zip
  Show-Summary | Out-Null
  exit 1
}

# 4. Tests
if ($SkipTests) {
  Add-Result "Pruebas Parts" "OMITIDO" "-SkipTests"
} else {
  $code = Invoke-Logged "Pruebas Parts.Core" ('dotnet test "' + (Join-Path $repoRoot $projects[0]) + '" -c Release --no-build') "test-core.log"
  if ($code -eq 0) { Add-Result "Pruebas Parts.Core" "PASS" "" } else { Add-Result "Pruebas Parts.Core" "FAIL" "test-core.log" }

  $code = Invoke-Logged "Pruebas Parts.Cli" ('dotnet test "' + (Join-Path $repoRoot $projects[2]) + '" -c Release --no-build') "test-cli.log"
  if ($code -eq 0) { Add-Result "Pruebas Parts.Cli" "PASS" "" } else { Add-Result "Pruebas Parts.Cli" "FAIL" "test-cli.log" }

  $env:PICOMOLDFORGE_PARTS_PICOGK_TESTS = "1"
  try {
    $code = Invoke-Logged "Pruebas PicoGK nativo (opt-in)" ('dotnet test "' + (Join-Path $repoRoot $projects[1]) + '" -c Release --no-build') "test-picogk.log"
  } finally {
    Remove-Item Env:\PICOMOLDFORGE_PARTS_PICOGK_TESTS -ErrorAction SilentlyContinue
  }
  if ($code -eq 0) { Add-Result "Pruebas PicoGK nativo" "PASS" "" } else { Add-Result "Pruebas PicoGK nativo" "FAIL" "test-picogk.log" }
}

if ($FullBaseline) {
  $code = Invoke-Logged "Pruebas de toda la solucion" ('dotnet test "' + (Join-Path $repoRoot "PicoMoldForge.sln") + '"') "test-solution.log"
  if ($code -eq 0) { Add-Result "Pruebas solucion completa" "PASS" "" } else { Add-Result "Pruebas solucion completa" "FAIL" "test-solution.log" }
}

# 5. Demo: reference mesher + PicoGK cross-check. If the PicoGK run cannot finish, regenerate with the reference only.
$code = Invoke-Logged "Demo (referencia + PicoGK)" ('dotnet "' + $cliDll + '" demo --backend both --clean-output --output "' + $outputDir + '"') "demo.log"
if ($code -eq 0) {
  Add-Result "Demo referencia + PicoGK" "PASS" "5 piezas verificadas"
} elseif ($code -eq 1) {
  Add-Result "Demo referencia + PicoGK" "FAIL" "alguna verificacion fallo: ver summary.txt"
} else {
  Add-Result "Demo referencia + PicoGK" "FAIL" ("codigo " + $code + ": se regenera solo con la referencia")
  $code = Invoke-Logged "Demo (solo referencia)" ('dotnet "' + $cliDll + '" demo --backend reference --clean-output --output "' + $outputDir + '"') "demo-reference.log"
  if ($code -eq 0) { Add-Result "Demo solo referencia" "PASS" "" } else { Add-Result "Demo solo referencia" "FAIL" "demo-reference.log" }
}

$summaryFile = Join-Path $outputDir "summary.txt"
if (Test-Path -LiteralPath $summaryFile) {
  Write-Host ""
  Get-Content -LiteralPath $summaryFile | ForEach-Object { Write-Host $_ }
}

# 6. Zip outputs and logs (with launcher-summary.txt) on the Desktop
Save-LauncherSummary
Save-Zip

# 7. Summary
$failed = Show-Summary

# 8. Viewer in its own window (close that window, or press Ctrl+C in it, to stop it)
if ((-not $NoViewer) -and (Test-Path -LiteralPath (Join-Path $outputDir "index.html"))) {
  Write-Host ""
  Write-Host ("Visor 'modo malla' en http://127.0.0.1:" + $Port + "/  (se abre en otra ventana; cierrala para terminarlo)") -ForegroundColor Cyan
  Start-Process -FilePath "dotnet" -ArgumentList ('"' + $cliDll + '" view --output "' + $outputDir + '" --port ' + $Port)
}

if ($failed -eq 0) { exit 0 } else { exit 1 }
