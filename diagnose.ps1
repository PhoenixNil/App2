# Shadowsocks 诊断脚本
Write-Host "====== Shadowsocks 代理诊断工具 ======" -ForegroundColor Cyan
Write-Host ""

# 1. 检查 sslocal 进程
Write-Host "[1] 检查 sslocal.exe 进程..." -ForegroundColor Yellow
$process = Get-Process -Name "sslocal" -ErrorAction SilentlyContinue
if ($process) {
    Write-Host "✓ sslocal.exe 正在运行 (PID: $($process.Id))" -ForegroundColor Green
} else {
    Write-Host "✗ sslocal.exe 未运行" -ForegroundColor Red
}
Write-Host ""

# 2. 检查端口监听
Write-Host "[2] 检查端口 10808 监听状态..." -ForegroundColor Yellow
$listening = Get-NetTCPConnection -LocalPort 10808 -State Listen -ErrorAction SilentlyContinue
if ($listening) {
    Write-Host "✓ 端口 10808 正在监听" -ForegroundColor Green
} else {
    Write-Host "✗ 端口 10808 未监听" -ForegroundColor Red
}
Write-Host ""

# 3. 检查配置文件
Write-Host "[3] 检查配置文件..." -ForegroundColor Yellow
$configPath = "$env:LOCALAPPDATA\App2\sslocal.json"
if (Test-Path $configPath) {
    Write-Host "✓ 配置文件存在: $configPath" -ForegroundColor Green
    Write-Host "配置内容:" -ForegroundColor Cyan
    Get-Content $configPath | ConvertFrom-Json | ConvertTo-Json -Depth 10
} else {
    Write-Host "✗ 配置文件不存在: $configPath" -ForegroundColor Red
}
Write-Host ""

# 4. 检查系统代理设置
Write-Host "[4] 检查系统代理设置..." -ForegroundColor Yellow
$regPath = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Internet Settings"
$proxyEnable = (Get-ItemProperty -Path $regPath -Name ProxyEnable -ErrorAction SilentlyContinue).ProxyEnable
$proxyServer = (Get-ItemProperty -Path $regPath -Name ProxyServer -ErrorAction SilentlyContinue).ProxyServer

if ($proxyEnable -eq 1) {
    Write-Host "✓ 系统代理已启用" -ForegroundColor Green
    Write-Host "  代理服务器: $proxyServer" -ForegroundColor Cyan
} else {
    Write-Host "✗ 系统代理未启用" -ForegroundColor Red
}
Write-Host ""

# 5. 测试本地 SOCKS5 连接
Write-Host "[5] 测试 SOCKS5 代理连接..." -ForegroundColor Yellow
try {
    $tcpClient = New-Object System.Net.Sockets.TcpClient
    $tcpClient.Connect("127.0.0.1", 10808)
    if ($tcpClient.Connected) {
        Write-Host "✓ 可以连接到 127.0.0.1:10808" -ForegroundColor Green
        $tcpClient.Close()
    }
} catch {
    Write-Host "✗ 无法连接到 127.0.0.1:10808" -ForegroundColor Red
    Write-Host "  错误: $($_.Exception.Message)" -ForegroundColor Red
}
Write-Host ""

# 6. 提示下一步
Write-Host "====== 测试建议 ======" -ForegroundColor Cyan
Write-Host "1. 如果以上都正常，请在浏览器中测试:" -ForegroundColor White
Write-Host "   - Chrome/Edge: 可能需要安装 SwitchyOmega 扩展" -ForegroundColor Gray
Write-Host "   - Firefox: 设置 → 网络设置 → SOCKS5 → 127.0.0.1:10808" -ForegroundColor Gray
Write-Host ""
Write-Host "2. 使用 curl 测试代理:" -ForegroundColor White
Write-Host '   curl --socks5 127.0.0.1:10808 https://www.google.com' -ForegroundColor Gray
Write-Host ""
Write-Host "3. 检查服务器配置是否正确（地址、端口、密码、加密方式）" -ForegroundColor White
Write-Host ""
