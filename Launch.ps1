$ErrorActionPreference = 'Stop'
try {
    $project = $PSScriptRoot
    $exe = Join-Path $project 'app/LazyRead.exe'
    if (-not (Test-Path -LiteralPath $exe)) { $exe = Join-Path $project 'src/LazyRead/bin/Release/net8.0-windows/LazyRead.exe' }
    if (-not (Test-Path -LiteralPath $exe)) { throw 'Run Setup.ps1 in the Lazy Read folder first.' }
    # This is the interactive application requested by the user; no console is opened.
    Start-Process -FilePath $exe -WorkingDirectory $project -WindowStyle Normal
}
catch {
    Add-Type -AssemblyName PresentationFramework
    [System.Windows.MessageBox]::Show($_.Exception.Message, 'Lazy Read') | Out-Null
}
