$exe = Join-Path $PSScriptRoot 'SysInfoTool.exe'
if (-not (Test-Path $exe)) { Write-Host "Error: SysInfoTool.exe not found" -ForegroundColor Red; exit 1 }

$cmd = @('--console')
if ($NoMask) { $cmd += '--no-mask' }
if ($SkipScan) { $cmd += '--skip-scan' }
if ($OutDir) { $cmd += "--out=$OutDir" }

& $exe @cmd
