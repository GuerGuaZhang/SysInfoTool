# =============================================================
# SysInfoTool 本地构建脚本（无需安装 .NET SDK）
# 使用 Windows 自带的 .NET Framework C# 编译器（csc.exe）直接编译，
# 零外部依赖，保持单文件 exe 特性。
#
# 用法：
#   .\build.ps1                 # 输出到仓库根目录 SysInfoTool.exe
#   .\build.ps1 -OutDir .\bin   # 输出到指定目录
#   .\build.ps1 -NoOptimize     # 调试构建（不优化，便于调试）
# =============================================================
param(
    [string]$OutDir = "",
    [switch]$NoOptimize
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
if ([string]::IsNullOrEmpty($OutDir)) { $OutDir = $root }

$csc = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) { $csc = 'C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe' }
if (-not (Test-Path $csc)) { throw '未找到 csc.exe（需要 .NET Framework 4.x）' }

$refs = @(
    'System.dll', 'System.Core.dll', 'System.Drawing.dll',
    'System.Windows.Forms.dll', 'System.Management.dll',
    'System.Xml.dll', 'System.Xml.Linq.dll', 'Microsoft.CSharp.dll'
) | ForEach-Object { '/reference:' + $_ }

$files = (Get-ChildItem -Path (Join-Path $root 'Source') -Recurse -File -Filter '*.cs' | Sort-Object FullName).FullName
if ($files.Count -eq 0) { throw '未找到任何 .cs 源文件' }

$out = Join-Path $OutDir 'SysInfoTool.exe'
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null

# ---- 编译前自动备份旧版本 ----
if (Test-Path $out) {
    $oldVer = (Get-Item $out).VersionInfo.FileVersion
    if ([string]::IsNullOrEmpty($oldVer)) { $oldVer = "unknown" }
    $backupName = "SysInfoTool_v${oldVer}_backup.exe"
    $backupPath = Join-Path $OutDir $backupName
    if (-not (Test-Path $backupPath)) {
        Copy-Item $out $backupPath -Force
        Write-Host ("已备份旧版本 -> {0}" -f $backupName)
    } else {
        Write-Host ("备份 {0} 已存在，跳过" -f $backupName)
    }
}

$args = @(
    '/nologo', '/target:winexe', '/codepage:65001',
    $(if ($NoOptimize) { '/debug+' } else { '/optimize+' }),
    "/out:$out"
) + $refs + $files

Write-Host ("编译 {0} 个源文件 -> {1}" -f $files.Count, $out)
& $csc @args
if ($LASTEXITCODE -ne 0) { throw ('编译失败，退出码 ' + $LASTEXITCODE) }

$size = (Get-Item $out).Length
Write-Host ("构建成功：{0}（{1:N0} 字节）" -f $out, $size)
