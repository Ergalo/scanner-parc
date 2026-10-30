# Compile le scanner avec le compilateur C# livré dans Windows (.NET Framework 4.8) : rien à installer.
# Produit dist/ergalo-releve.exe (il refuse de tourner tant que son nom ne porte pas la marque donnée par le site au
# téléchargement), compile et lance les tests, puis écrit l'empreinte SHA-256 de l'exécutable dans dist/SHA256.txt.
#   powershell -ExecutionPolicy Bypass -File construire.ps1
$ErrorActionPreference = "Stop"
$ici = Split-Path -Parent $MyInvocation.MyCommand.Path
$csc = Join-Path $env:WINDIR "Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if (-not (Test-Path $csc)) { throw "Compilateur C# introuvable : $csc" }

$sources = Get-ChildItem (Join-Path $ici "src") -Filter *.cs | ForEach-Object { $_.FullName }
$dist = Join-Path $ici "dist"
$tmp = Join-Path $ici "obj"
New-Item -ItemType Directory -Force $dist, $tmp | Out-Null
$references = @("System.dll", "System.Core.dll", "System.Drawing.dll", "System.Windows.Forms.dll", "System.Web.Extensions.dll")
$refs = $references | ForEach-Object { "/reference:$_" }
$catalogue = "/resource:$(Join-Path $ici 'catalogue.tsv'),catalogue.tsv"

# 1. Le programme.
$exe = Join-Path $dist "ergalo-releve.exe"
& $csc /nologo /target:winexe /platform:anycpu /optimize+ /utf8output /codepage:65001 `
  "/win32manifest:$(Join-Path $ici 'src\app.manifest')" "/win32icon:$(Join-Path $ici 'src\ergalo.ico')" "/out:$exe" $refs $catalogue $sources
if ($LASTEXITCODE -ne 0) { throw "Compilation du programme en échec" }

# 2. Les tests : les mêmes sources (sauf le point d'entrée) et le lanceur de tests.
$sourcesTests = ($sources | Where-Object { -not $_.EndsWith("Programme.cs") }) + (Get-ChildItem (Join-Path $ici "tests") -Filter *.cs | ForEach-Object { $_.FullName })
$tests = Join-Path $tmp "tests.exe"
& $csc /nologo /target:exe /platform:anycpu /utf8output /codepage:65001 "/out:$tests" $refs $catalogue $sourcesTests
if ($LASTEXITCODE -ne 0) { throw "Compilation des tests en échec" }
& $tests (Join-Path $ici "tests\fictif") $exe
if ($LASTEXITCODE -ne 0) { throw "Tests en échec" }

$empreinte = (Get-FileHash $exe -Algorithm SHA256).Hash.ToLowerInvariant()
Set-Content -Path (Join-Path $dist "SHA256.txt") -Value "$empreinte  ergalo-releve.exe" -Encoding ascii
Write-Host "Programme : $exe ($((Get-Item $exe).Length) octets), SHA-256 $empreinte"
