$ErrorActionPreference = 'Stop'
$project = $PSScriptRoot
Set-Location -LiteralPath $project
function Checked([string]$program, [string[]]$arguments) {
    & $program @arguments
    if ($LASTEXITCODE -ne 0) { throw "$program failed with exit code $LASTEXITCODE" }
}
# A source checkout publishes the app; a release package already contains app/LazyRead.exe.
$fromSource = Test-Path -LiteralPath 'src/LazyRead/LazyRead.csproj'
if ($fromSource -and -not (Get-Command dotnet -ErrorAction SilentlyContinue)) { throw 'Install the .NET 8 SDK from https://dotnet.microsoft.com/download/dotnet/8.0, then run Setup.ps1 again.' }
if (-not $fromSource -and -not (Test-Path -LiteralPath 'app/LazyRead.exe')) { throw 'app/LazyRead.exe is missing. Extract the complete Lazy Read release zip, then run Setup.ps1 again.' }
if (-not (Test-Path -LiteralPath '.venv/Scripts/python.exe')) {
    if (-not (Get-Command py -ErrorAction SilentlyContinue)) { throw 'Install Python 3.10 (including the Python launcher), for example with: winget install Python.Python.3.10. Then run Setup.ps1 again.' }
    Checked 'py' @('-3.10', '-m', 'venv', '.venv')
}
$python = Join-Path $project '.venv/Scripts/python.exe'
Write-Host 'Installing the local speech engine. No administrator access is needed.'
Checked $python @('-m', 'pip', 'install', '-r', 'engine/requirements.txt')
if ($fromSource) {
    Checked 'dotnet' @('publish', 'src/LazyRead/LazyRead.csproj', '-c', 'Release', '-r', 'win-x64', '--self-contained', 'true', '-p:PublishSingleFile=true', '-p:IncludeNativeLibrariesForSelfExtract=true', '-o', 'app')
}
Write-Host 'Ready. Double-click Start Lazy Read.cmd. The first reading downloads the Kokoro model (about 350 MB); later readings use the local copy.'
