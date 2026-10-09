param([switch]$Cpu)
$ErrorActionPreference = 'Stop'
$project = $PSScriptRoot
Set-Location -LiteralPath $project
function Checked([string]$program, [string[]]$arguments) {
    & $program @arguments
    if ($LASTEXITCODE -ne 0) { throw "$program failed with exit code $LASTEXITCODE" }
}
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) { throw 'Install the .NET 8 SDK from https://dotnet.microsoft.com/download/dotnet/8.0, then run Setup.ps1 again.' }
if (-not (Test-Path -LiteralPath '.venv/Scripts/python.exe')) {
    if (-not (Get-Command py -ErrorAction SilentlyContinue)) { throw 'Install Python 3.10 (including the Python launcher), then run Setup.ps1 again.' }
    Checked 'py' @('-3.10', '-m', 'venv', '.venv')
}
$python = Join-Path $project '.venv/Scripts/python.exe'
$wheelIndex = if ($Cpu) { 'https://download.pytorch.org/whl/cpu' } else { 'https://download.pytorch.org/whl/cu128' }
Write-Host 'Installing the local speech engine. No administrator access is needed.'
Checked $python @('-m', 'pip', 'install', 'torch==2.9.1', 'torchaudio==2.9.1', '--index-url', $wheelIndex)
Checked $python @('-m', 'pip', 'install', '-r', 'engine/requirements.txt')
Checked 'dotnet' @('publish', 'src/LazyRead/LazyRead.csproj', '-c', 'Release', '-r', 'win-x64', '--self-contained', 'true', '-p:PublishSingleFile=true', '-p:IncludeNativeLibrariesForSelfExtract=true', '-o', 'app')
Write-Host 'Ready. Double-click Start Lazy Read.cmd. The first launch downloads the Qwen3 model; subsequent launches use its cache.'
