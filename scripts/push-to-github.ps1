Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

Set-Location "C:\repos\PicoMoldForge"

$owner = "crasofuentes-hub"
$repo = "PicoMoldForge"
$repoFullName = "$owner/$repo"
$repoHttps = "https://github.com/$repoFullName.git"

Write-Host "`n== VERIFY TOOLS ==" -ForegroundColor Cyan
git --version
gh --version

Write-Host "`n== VERIFY GITHUB AUTH ==" -ForegroundColor Cyan
gh auth status

Write-Host "`n== VERIFY PROJECT ROOT ==" -ForegroundColor Cyan

$required = @(
  ".\PicoMoldForge.sln",
  ".\src\PicoMoldForge.Generator\PicoMoldForge.Generator.csproj",
  ".\scripts\verify-baseline.ps1",
  ".\scripts\verify-generator-publish.ps1",
  ".\docs\GENERATOR_USAGE.md",
  ".\samples\generator-valid-project.json",
  ".\samples\generator-sample-binary.stl"
)

foreach ($path in $required) {
  if (-not (Test-Path $path)) {
    throw "Missing required file: $path"
  }

  Write-Host "OK $path" -ForegroundColor Green
}

Write-Host "`n== INIT LOCAL GIT ==" -ForegroundColor Cyan

if (-not (Test-Path ".\.git")) {
  git init
}

git branch -M main

Write-Host "`n== WRITE .GITIGNORE ==" -ForegroundColor Cyan

$gitignore = @(
  "bin/",
  "obj/",
  ".vs/",
  ".vscode/",
  "*.user",
  "*.suo",
  "publish/",
  "samples/generated/",
  "output/",
  "*.log",
  "PicoGK.log",
  "TestResults/",
  "coverage/",
  "Thumbs.db",
  ".DS_Store"
)

$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
[System.IO.File]::WriteAllLines("C:\repos\PicoMoldForge\.gitignore", $gitignore, $utf8NoBom)

Write-Host "`n== CONFIGURE GIT IDENTITY ==" -ForegroundColor Cyan

if ([string]::IsNullOrWhiteSpace((git config user.name))) {
  git config user.name "Oscar Fuentes"
}

if ([string]::IsNullOrWhiteSpace((git config user.email))) {
  git config user.email "crasofuentes@gmail.com"
}

git config user.name
git config user.email

Write-Host "`n== RECREATE LOCAL COMMIT ==" -ForegroundColor Cyan

git add .

$staged = git diff --cached --name-only

if ([string]::IsNullOrWhiteSpace($staged)) {
  Write-Host "No staged changes. Checking existing commits..." -ForegroundColor Yellow

  git rev-parse --verify HEAD *> $null

  if ($LASTEXITCODE -ne 0) {
    throw "No staged changes and no existing commit. Cannot push."
  }
}
else {
  git commit -m "feat: add PicoMoldForge v5 generator"
}

Write-Host "`n== VERIFY OR CREATE GITHUB REPO ==" -ForegroundColor Cyan

gh repo view $repoFullName *> $null

if ($LASTEXITCODE -ne 0) {
  Write-Host "Remote repo does not exist. Creating: $repoFullName" -ForegroundColor Yellow
  gh repo create $repoFullName --private
}
else {
  Write-Host "Remote repo already exists: $repoFullName" -ForegroundColor Green
}

Write-Host "`n== CONFIGURE ORIGIN ==" -ForegroundColor Cyan

git remote get-url origin *> $null

if ($LASTEXITCODE -eq 0) {
  git remote set-url origin $repoHttps
}
else {
  git remote add origin $repoHttps
}

git remote -v

Write-Host "`n== PUSH MAIN ==" -ForegroundColor Cyan

git push -u origin main

Write-Host "`n== FINAL STATE ==" -ForegroundColor Cyan
git status --short
git log --oneline -5
git remote -v

Write-Host "`n== DONE ==" -ForegroundColor Green
Write-Host "Repo: https://github.com/$repoFullName" -ForegroundColor Green