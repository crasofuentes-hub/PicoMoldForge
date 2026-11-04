# PicoMoldForge - full mold verification for Windows PowerShell 5.1.
#
# Rebuilds the solution from scratch (Release, --no-incremental) and runs every test project (PicoGK native
# tests included). It then designs the five Parts samples, generates a mold for each of them and for the
# repository sample (the 20 x 10 x 5 mm box), and collects the verdict of every mold. Logs, reports and the
# halves of the box and the spur gear go into one ZIP on the Desktop.
#
# The build is never incremental: MSBuild skips a project whose outputs look newer than its sources, so with a
# clock that runs behind (the sources git writes look older than the last build) it keeps old binaries, and a
# project that does compile meets types that are not there. The summary says when the clock is behind the
# commit.
#
# Usage (from the repository root):
#   powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\run-mold-verification.ps1
# Options:
#   -SkipTests      do not run the test projects
#
# Exit code: 0 when every step passed (build, tests, parts, every mold generated). The verdict of each mold
# (PASS, or FAIL with the zones that need more than two halves) is a result reported in the summary.

param(
  [switch]$SkipTests
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot "..")).Path
Set-Location -LiteralPath $repoRoot

$stamp = Get-Date -Format "yyyyMMdd-HHmmss"
$runDir = Join-Path $repoRoot (Join-Path "mold-run" $stamp)
$logDir = Join-Path $runDir "logs"
$partsDir = Join-Path $runDir "parts"
$moldsDir = Join-Path $runDir "molds"
$partsCli = Join-Path $repoRoot "src\PicoMoldForge.Parts.Cli\bin\Release\net9.0\PicoMoldForge.Parts.Cli.dll"
$generator = Join-Path $repoRoot "src\PicoMoldForge.Generator\bin\Release\net9.0\PicoMoldForge.Generator.dll"
$results = New-Object System.Collections.Generic.List[object]
$molds = New-Object System.Collections.Generic.List[object]
$notes = New-Object System.Collections.Generic.List[string]

New-Item -ItemType Directory -Force -Path $logDir | Out-Null
New-Item -ItemType Directory -Force -Path $partsDir | Out-Null
New-Item -ItemType Directory -Force -Path $moldsDir | Out-Null

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

# Unix time of the commit of HEAD, read from the committer line of the commit object; 0 when git cannot tell.
function Get-HeadCommitTime {
  $lines = @(cmd /c ('git -C "' + $repoRoot + '" cat-file -p HEAD 2>nul'))
  foreach ($line in $lines) {
    if ($line.StartsWith("committer ")) {
      $parts = $line.Split(' ')
      $seconds = [long]0
      if (($parts.Count -ge 3) -and [long]::TryParse($parts[$parts.Count - 2], [ref]$seconds)) { return $seconds }
    }
  }
  return [long]0
}

# First line of the log that starts with the prefix, without the prefix; empty when there is none.
function Get-LogLine {
  param([string]$LogName, [string]$Prefix)
  $logFile = Join-Path $logDir $LogName
  if (-not (Test-Path -LiteralPath $logFile)) { return "" }
  foreach ($line in (Get-Content -LiteralPath $logFile)) {
    if ($line.StartsWith($Prefix)) { return $line.Substring($Prefix.Length).Trim() }
  }
  return ""
}

function Save-LauncherSummary {
  try {
    $lines = New-Object System.Collections.Generic.List[string]
    $lines.Add("PicoMoldForge - verificacion de moldes")
    $lines.Add("Fecha:       " + (Get-Date -Format "yyyy-MM-dd HH:mm:ss zzz"))
    $lines.Add("Repositorio: " + $repoRoot)
    $commit = @(cmd /c ('git -C "' + $repoRoot + '" rev-parse HEAD 2>nul'))
    if ($commit.Count -gt 0) { $lines.Add("Commit:      " + $commit[0]) } else { $lines.Add("Commit:      (no disponible)") }
    foreach ($note in $notes) { $lines.Add($note) }
    $lines.Add("")
    $lines.Add(($results | Format-Table -AutoSize | Out-String -Width 240).TrimEnd())
    $lines.Add("")
    $lines.Add("Moldes:")
    $lines.Add(($molds | Format-Table -AutoSize -Wrap | Out-String -Width 240).TrimEnd())
    [System.IO.File]::WriteAllLines((Join-Path $logDir "launcher-summary.txt"), $lines)
  } catch {
    Write-Host ("No se pudo escribir launcher-summary.txt: " + $_.Exception.Message) -ForegroundColor Yellow
  }
}

# One zip on the Desktop: logs, reports and text of the whole run, plus the mold halves of the box and the spur
# gear (the other STL files stay in the run folder; files.txt lists them). Entry names use "/".
function Save-Zip {
  try {
    Add-Type -AssemblyName System.IO.Compression
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $listing = New-Object System.Collections.Generic.List[string]
    foreach ($file in (Get-ChildItem -LiteralPath $runDir -Recurse -File)) {
      $listing.Add($file.FullName.Substring($runDir.Length).TrimStart('\') + "`t" + $file.Length)
    }
    [System.IO.File]::WriteAllLines((Join-Path $logDir "files.txt"), $listing)
    $desktop = [Environment]::GetFolderPath("Desktop")
    $zipPath = Join-Path $desktop ("PicoMoldForge-Moldes-" + $stamp + ".zip")
    if (Test-Path -LiteralPath $zipPath) { Remove-Item -LiteralPath $zipPath -Force }
    $keepStl = @("MovingHalf.stl", "FixedHalf.stl")
    $archive = [System.IO.Compression.ZipFile]::Open($zipPath, [System.IO.Compression.ZipArchiveMode]::Create)
    try {
      foreach ($file in (Get-ChildItem -LiteralPath $runDir -Recurse -File)) {
        $relative = $file.FullName.Substring($runDir.Length).TrimStart('\')
        $isStl = $file.Extension -ieq ".stl"
        $isKeptHalf = $isStl -and ($keepStl -contains $file.Name) -and (($relative -like "molds\sample-box\*") -or ($relative -like "molds\spur-gear\*"))
        $isBinary = $file.Extension -ieq ".bin"
        if (($isStl -and -not $isKeptHalf) -or $isBinary) { continue }
        $entryName = $relative.Replace('\', '/')
        [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $file.FullName, $entryName, [System.IO.Compression.CompressionLevel]::Optimal) | Out-Null
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
  $results | Format-Table -AutoSize | Out-String -Width 240 | Write-Host
  Write-Host "== MOLDES" -ForegroundColor Cyan
  $molds | Format-Table -AutoSize -Wrap | Out-String -Width 240 | Write-Host
  $failedSteps = @($results | Where-Object { $_.Estado -eq "FAIL" }).Count
  if ($failedSteps -eq 0) {
    Write-Host "VERIFICACION DE MOLDES: todos los pasos corrieron" -ForegroundColor Green
  } else {
    Write-Host ("VERIFICACION DE MOLDES: " + $failedSteps + " paso(s) con falla") -ForegroundColor Red
  }
  return $failedSteps
}

Write-Host "PicoMoldForge - verificacion de moldes" -ForegroundColor Cyan
Write-Host ("Repositorio: " + $repoRoot)
Write-Host ("Corrida:     " + $runDir)

# 1. Tooling (dotnet from PATH, or from its default folder; English messages so the logs read the same everywhere)
$env:DOTNET_CLI_UI_LANGUAGE = "en"
$env:DOTNET_NOLOGO = "1"
$dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
if (($null -eq $dotnet) -and (Test-Path -LiteralPath "C:\Program Files\dotnet\dotnet.exe")) {
  $env:PATH = "C:\Program Files\dotnet;" + $env:PATH
  $dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
}
if ($null -eq $dotnet) {
  Write-Host "No se encontro 'dotnet'. Instala el .NET SDK 9 y vuelve a correr este script." -ForegroundColor Red
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

# The clock: a commit dated after "now" means the clock of this computer runs behind. The full rebuild below does
# not depend on it; the row says so, with both dates.
$commitTime = Get-HeadCommitTime
$nowTime = [DateTimeOffset]::UtcNow.ToUnixTimeSeconds()
$nowLocal = Get-Date -Format "yyyy-MM-dd HH:mm"
if ($commitTime -le 0) {
  Add-Result "Reloj" "AVISO" ("no se pudo leer la fecha del commit; el reloj marca " + $nowLocal)
} elseif ($commitTime -gt ($nowTime + 300)) {
  $commitLocal = [DateTimeOffset]::FromUnixTimeSeconds($commitTime).ToLocalTime().ToString("yyyy-MM-dd HH:mm")
  Add-Result "Reloj" "AVISO" ("atrasado: el commit es del " + $commitLocal + " y el reloj marca " + $nowLocal + "; se compila todo desde cero")
} else {
  Add-Result "Reloj" "PASS" ("no esta atrasado respecto al commit; marca " + $nowLocal)
}

# 2. A viewer or a generator left running keeps its files open and the build cannot replace them.
$running = @()
try {
  $running = @(Get-CimInstance -ClassName Win32_Process -Filter "Name = 'dotnet.exe'" -ErrorAction Stop |
    Where-Object { $_.CommandLine -and (($_.CommandLine.IndexOf("PicoMoldForge.Parts.Cli.dll", [System.StringComparison]::OrdinalIgnoreCase) -ge 0) -or ($_.CommandLine.IndexOf("PicoMoldForge.Generator.dll", [System.StringComparison]::OrdinalIgnoreCase) -ge 0)) })
} catch {
  $notes.Add("No se pudo revisar si habia programas abiertos: " + $_.Exception.Message)
}
foreach ($process in $running) {
  Write-Host ("Cerrando una ejecucion anterior (PID " + $process.ProcessId + ").") -ForegroundColor Yellow
  $notes.Add("Cerrada una ejecucion anterior: PID " + $process.ProcessId + " | " + $process.CommandLine)
  Stop-Process -Id $process.ProcessId -Force -ErrorAction SilentlyContinue
}
if ($running.Count -gt 0) { Start-Sleep -Seconds 2 }

# 3. Build, from scratch (see the header: an incremental build trusts the clock)
$code = Invoke-Logged "Compilar la solucion desde cero (Release)" ('dotnet build "' + (Join-Path $repoRoot "PicoMoldForge.sln") + '" -c Release --no-incremental') "build.log"
if ($code -ne 0) {
  if (Select-String -LiteralPath (Join-Path $logDir "build.log") -Pattern "MSB3027|MSB3021|being used by another process" -Quiet) {
    Add-Result "Compilacion" "FAIL" "otro programa tiene abierto un archivo de la compilacion (el log dice cual): cierralo y vuelve a correr"
  } else {
    Add-Result "Compilacion" "FAIL" "ver logs\build.log"
  }
  Save-LauncherSummary
  Save-Zip
  Show-Summary | Out-Null
  exit 1
}
Add-Result "Compilacion" "PASS" "Release, desde cero"

# 4. Tests of every project, the PicoGK native tests of Parts included (opt-in variable)
if ($SkipTests) {
  Add-Result "Pruebas" "OMITIDO" "-SkipTests"
} else {
  $env:PICOMOLDFORGE_PARTS_PICOGK_TESTS = "1"
  try {
    $code = Invoke-Logged "Pruebas de toda la solucion" ('dotnet test "' + (Join-Path $repoRoot "PicoMoldForge.sln") + '" -c Release --no-build --logger trx --results-directory "' + (Join-Path $logDir "trx") + '"') "test.log"
  } finally {
    Remove-Item Env:\PICOMOLDFORGE_PARTS_PICOGK_TESTS -ErrorAction SilentlyContinue
  }
  # One TRX file per test project: its counters say how many tests passed and failed.
  $trxDir = Join-Path $logDir "trx"
  $trxFiles = @()
  if (Test-Path -LiteralPath $trxDir) { $trxFiles = @(Get-ChildItem -LiteralPath $trxDir -Filter "*.trx" -File) }
  foreach ($trx in $trxFiles) {
    try {
      [xml]$doc = Get-Content -LiteralPath $trx.FullName -Raw
      $counters = $doc.TestRun.ResultSummary.Counters
      $storage = @($doc.TestRun.TestDefinitions.UnitTest)[0].storage
      $name = [System.IO.Path]::GetFileNameWithoutExtension($storage)
      $status = "PASS"
      if ([int]$counters.failed -gt 0) { $status = "FAIL" }
      Add-Result ("Pruebas " + $name) $status ($counters.passed + " de " + $counters.total + " pasan, " + $counters.failed + " fallan")
    } catch {
      Add-Result ("Pruebas " + $trx.Name) "FAIL" ("no se pudo leer el TRX: " + $_.Exception.Message)
    }
  }
  if ($trxFiles.Count -eq 0) { Add-Result "Pruebas" "FAIL" ("sin resultados TRX; codigo " + $code + ": ver logs\test.log") }
  if ($code -ne 0) { $notes.Add("dotnet test termino con codigo " + $code + "; ver logs\test.log y logs\trx") }
}

# 5. The five Parts samples (reference mesher)
$code = Invoke-Logged "Disenar las piezas de ejemplo (Parts)" ('dotnet "' + $partsCli + '" demo --backend reference --clean-output --output "' + $partsDir + '"') "parts.log"
if ($code -eq 0) { Add-Result "Piezas de ejemplo" "PASS" "5 piezas verificadas" } else { Add-Result "Piezas de ejemplo" "FAIL" ("codigo " + $code + ": ver logs\parts.log") }

# 6. Molds: the repository sample (the box) and each designed part
$jobs = New-Object System.Collections.Generic.List[object]
$jobs.Add([pscustomobject]@{ Name = "sample-box"; Args = ('--config "' + (Join-Path $repoRoot "samples\generator-valid-project.json") + '" --generate-all --clean-output --output "' + (Join-Path $moldsDir "sample-box") + '"') }) | Out-Null
foreach ($id in @("spur-gear", "helical-gear", "flange", "piston", "monoblock")) {
  $stl = Join-Path $partsDir (Join-Path $id ($id + ".stl"))
  if (Test-Path -LiteralPath $stl) {
    $jobs.Add([pscustomobject]@{ Name = $id; Args = ('--mold-stl "' + $stl + '" --output "' + (Join-Path $moldsDir $id) + '" --clean-output') }) | Out-Null
  } else {
    Add-Result ("Molde " + $id) "FAIL" "la pieza no se genero"
  }
}
foreach ($job in $jobs) {
  $logName = "mold-" + $job.Name + ".log"
  $started = Get-Date
  $code = Invoke-Logged ("Molde " + $job.Name) ('dotnet "' + $generator + '" ' + $job.Args) $logName
  $seconds = [int]((Get-Date) - $started).TotalSeconds
  $verdict = Get-LogLine $logName "Mold verification:"
  $artifacts = Get-LogLine $logName "Artifacts generated:"
  if ($code -eq 0) {
    Add-Result ("Molde " + $job.Name) "PASS" ($artifacts + " archivos, " + $seconds + " s")
  } else {
    Add-Result ("Molde " + $job.Name) "FAIL" ("codigo " + $code + ": ver logs\" + $logName)
  }
  if ([string]::IsNullOrEmpty($verdict)) { $verdict = "(sin veredicto)" }
  $molds.Add([pscustomobject]@{ Molde = $job.Name; Veredicto = $verdict }) | Out-Null
}

# 7. Summary file, ZIP and summary
Save-LauncherSummary
Save-Zip
$failed = Show-Summary
if ($failed -eq 0) { exit 0 } else { exit 1 }
