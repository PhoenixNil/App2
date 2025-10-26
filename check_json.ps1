# 验证 JSON 文件生成脚本
Write-Host "====== JSON 文件检查 ======" -ForegroundColor Cyan
Write-Host ""

$appDataPath = "$env:LOCALAPPDATA\App2"

Write-Host "应用数据目录: $appDataPath" -ForegroundColor Yellow
Write-Host ""

if (Test-Path $appDataPath) {
    Write-Host "✓ 目录存在" -ForegroundColor Green
    Write-Host ""

    $files = Get-ChildItem $appDataPath -ErrorAction SilentlyContinue

    if ($files.Count -eq 0) {
        Write-Host "⚠ 目录为空（还未生成文件）" -ForegroundColor Yellow
    } else {
        Write-Host "找到以下文件:" -ForegroundColor Green
        $files | Format-Table Name, Length, LastWriteTime -AutoSize

        # 显示 servers.json 内容
        $serversPath = Join-Path $appDataPath "servers.json"
        if (Test-Path $serversPath) {
            Write-Host ""
            Write-Host "=== servers.json 内容 ===" -ForegroundColor Cyan
            Get-Content $serversPath
        }

        # 显示 sslocal.json 内容
        $sslocalPath = Join-Path $appDataPath "sslocal.json"
        if (Test-Path $sslocalPath) {
            Write-Host ""
            Write-Host "=== sslocal.json 内容 ===" -ForegroundColor Cyan
            Get-Content $sslocalPath
        }
    }
} else {
    Write-Host "✗ 目录不存在（还未运行过应用）" -ForegroundColor Red
    Write-Host ""
    Write-Host "💡 提示：" -ForegroundColor Yellow
    Write-Host "  1. 运行应用" -ForegroundColor Gray
    Write-Host "  2. 添加一个服务器节点" -ForegroundColor Gray
    Write-Host "  3. 重新运行此脚本" -ForegroundColor Gray
}

Write-Host ""
Write-Host "====== 完成 ======" -ForegroundColor Cyan
