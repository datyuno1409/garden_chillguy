# Cập nhật knowledge graph của Graphify cho project Unity.
# Graphify luôn ghi kết quả vào Assets\graphify-out, nhưng thư mục nằm trong Assets sẽ bị Unity import,
# nên script này chuyển kết quả ra graphify-out ở thư mục gốc project.
$ErrorActionPreference = 'Stop'
$env:GRAPHIFY_NO_AUTO_REFRESH = '1'
$graphify = Join-Path $env:USERPROFILE '.local\bin\graphify.exe'
$root = $PSScriptRoot
$inAssets = Join-Path $root 'Assets\graphify-out'
$outside = Join-Path $root 'graphify-out'

Set-Location $root

# Graphify in cảnh báo ra stderr (ví dụ skill của nền tảng khác cũ hơn). PowerShell 5.1 coi đó là lỗi
# nên chỉ kiểm tra mã thoát thật của lệnh.
$ErrorActionPreference = 'Continue'
& $graphify update Assets
$exitCode = $LASTEXITCODE
$ErrorActionPreference = 'Stop'
if ($exitCode -ne 0) { throw "graphify update thất bại (mã thoát $exitCode)" }

if (Test-Path $inAssets) {
    New-Item -ItemType Directory -Force $outside | Out-Null
    Copy-Item (Join-Path $inAssets '*') $outside -Recurse -Force
    Remove-Item $inAssets -Recurse -Force
    $meta = "$inAssets.meta"
    if (Test-Path $meta) { Remove-Item $meta -Force }
}
Write-Host "Graph đã cập nhật tại $outside"
