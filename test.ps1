param([switch]$Gpu, [switch]$Ui, [switch]$Rtx)
$ErrorActionPreference = 'Stop'
Push-Location $PSScriptRoot
try {
    dotnet build WindowsMlDemo.csproj -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
    $executable = Join-Path $PSScriptRoot 'bin\Release\net10.0-windows10.0.19041.0\win-x64\WindowsMlDemo.exe'
    $arguments = @('--self-test')
    if ($Gpu) { $arguments += '--gpu' }
    if ($Rtx) { $arguments += '--rtx' }
    if ($Ui) { $arguments = @('--ui-test') }
    $process = Start-Process -FilePath $executable -ArgumentList $arguments -WindowStyle Hidden -Wait -PassThru
    $reportName = if ($Ui) { 'ui-test.txt' } else { 'self-test.txt' }
    Get-Content -LiteralPath (Join-Path (Split-Path $executable) $reportName)
    if ($process.ExitCode -ne 0) { throw "Self-test failed with code $($process.ExitCode)" }
} finally { Pop-Location }
