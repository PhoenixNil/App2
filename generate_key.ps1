# 生成 SS2022 密钥的 PowerShell 脚本

# 生成 32 字节随机密钥并转换为 base64
$bytes = New-Object byte[] 32
[System.Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($bytes)
$key = [Convert]::ToBase64String($bytes)

Write-Host "生成的 SS2022 密钥 (2022-blake3-aes-256-gcm):" -ForegroundColor Green
Write-Host $key -ForegroundColor Yellow
Write-Host ""
Write-Host "请复制此密钥到应用的密码框中" -ForegroundColor Cyan
