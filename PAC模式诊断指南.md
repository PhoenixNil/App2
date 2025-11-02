# PAC 模式诊断指南

## 您遇到的错误分析

根据您提供的日志：
```
[00:33:15] sslocal 已启动，PID: 21292
[00:33:15] [INFO] shadowsocks socks TCP listening on 127.0.0.1:10808    
[00:33:18] [INFO] ERROR HTTP connection 127.0.0.1:8297 handler failed with error: error from user's Service    
[00:33:18] [INFO] ERROR socks5 tcp client handler error: error from user's Service
```

### 错误解读

✅ **正常的部分**：
- Shadowsocks 本地服务已成功启动
- SOCKS5 代理正在 127.0.0.1:10808 监听
- PAC 服务器应该在 127.0.0.1:7090 运行

❌ **错误的部分**：
- `error from user's Service` 表示 Shadowsocks 无法连接到远程服务器
- 这**不是 PAC 配置问题**，而是 Shadowsocks 连接问题

## 可能的原因

### 1. 服务器配置错误
- ❌ 密码不正确
- ❌ 加密方式不匹配
- ❌ 服务器地址或端口错误

### 2. 网络连接问题
- ❌ 远程服务器无法访问
- ❌ 服务器已过期或停止服务
- ❌ 本地网络阻止连接

### 3. 防火墙/安全软件
- ❌ Windows 防火墙阻止
- ❌ 杀毒软件拦截
- ❌ 企业网络策略限制

## 诊断步骤

### 步骤 1：验证 PAC 服务器是否运行

1. 重新启动应用并启动 PAC 模式
2. 在"设置"菜单中选择"查看日志"
3. 查找包含 `[PAC]` 的日志条目：
   ```
   [00:XX:XX] [PAC] 正在启动 PAC 服务器，端口: 7090
   [00:XX:XX] [PAC] PAC 内容已生成，长度: XXXXX 字符
   [00:XX:XX] [PAC] PAC 服务器已启动: http://127.0.0.1:7090/pac.js
   ```

4. 打开浏览器，访问 `http://127.0.0.1:7090/pac.js`
   - ✅ 如果能看到 JavaScript 代码 → PAC 服务器正常
   - ❌ 如果无法访问 → PAC 服务器未启动

### 步骤 2：检查服务器配置

#### 验证服务器信息
1. 确认服务器地址、端口、密码、加密方式都正确
2. 如果是从 SS 链接导入，尝试重新导入
3. 如果是手动添加，仔细检查每个字段

#### 测试服务器连通性
1. 点击"测试延迟"按钮
2. 查看延迟结果：
   - ✅ 显示延迟（如 "123 ms"） → 服务器可连接
   - ❌ 显示"超时"或"测试失败" → 服务器不可用

### 步骤 3：测试全局模式

1. 切换到"全局代理"模式
2. 尝试访问网站
3. 如果全局模式也失败 → 确认是服务器配置问题
4. 如果全局模式成功 → 返回 PAC 模式继续测试

### 步骤 4：查看系统代理设置

1. 打开 Windows 设置 → 网络和 Internet → 代理
2. 在 PAC 模式下应该看到：
   - 自动检测设置：关闭
   - 使用设置脚本：**开启**
   - 脚本地址：`http://127.0.0.1:7090/pac.js`

### 步骤 5：测试 PAC 规则

1. 访问国内网站（如 www.baidu.com）：
   - 应该直连（不通过代理）
   - 速度正常

2. 访问 PAC 列表中的网站（如 www.google.com）：
   - 应该通过代理
   - 如果服务器正常，可以访问

## 常见问题解决

### Q1: PAC 服务器未启动
**症状**：访问 `http://127.0.0.1:7090/pac.js` 失败

**解决方法**：
1. 检查端口 7090 是否被占用
   ```powershell
   netstat -ano | findstr :7090
   ```
2. 如果被占用，修改 `Services/PACServerService.cs` 中的默认端口
3. 查看应用日志中的错误信息

### Q2: "error from user's Service" 错误
**症状**：日志显示 socks5 tcp client handler error

**这是最常见的问题！可能原因：**

#### A. 服务器配置错误
```
解决：
1. 重新导入 SS 链接
2. 确认密码、加密方式正确
3. 联系服务提供商确认服务状态
```

#### B. 服务器过期或停止
```
解决：
1. 联系服务提供商续费
2. 更换其他可用节点
3. 测试延迟确认服务器状态
```

#### C. SS2022 密钥格式问题
```
症状：使用 2022-blake3-* 加密方式
解决：
1. 确保密钥是 Base64 编码
2. 确保密钥长度正确：
   - aes-128: 16 字节 Base64
   - aes-256: 32 字节 Base64
3. 使用 generate_key.ps1 生成正确的密钥
```

### Q3: PAC 模式不生效
**症状**：代理设置了但网站无法访问

**解决方法**：
1. 确认浏览器使用系统代理设置
2. 清除浏览器缓存和 DNS 缓存：
   ```powershell
   ipconfig /flushdns
   ```
3. 重启浏览器
4. 尝试全局模式确认服务器可用

### Q4: 部分网站无法访问
**症状**：某些网站无法通过代理访问

**解决方法**：
1. 检查网站域名是否在 pac.txt 规则列表中
2. 如需添加域名，编辑 pac.txt 文件
3. 重新启动代理服务

## 建议的测试流程

### 🔍 完整诊断流程

```
1. 测试服务器延迟
   ↓
   延迟正常？
   ↓ 是
2. 启动全局代理模式
   ↓
   可以访问 Google？
   ↓ 是
3. 切换到 PAC 模式
   ↓
4. 查看应用日志
   ↓
   看到 "[PAC] PAC 服务器已启动"？
   ↓ 是
5. 访问 http://127.0.0.1:7090/pac.js
   ↓
   能看到 PAC 脚本？
   ↓ 是
6. 检查系统代理设置
   ↓
   "使用设置脚本" 已启用？
   ↓ 是
7. 测试访问 Google
   ↓
   成功！✅
```

## 获取详细日志

如果问题仍然存在，请收集以下信息：

1. **应用日志**：
   - 点击"设置" → "查看日志"
   - 复制全部日志

2. **系统代理设置截图**：
   - Windows 设置 → 网络和 Internet → 代理

3. **PAC 文件内容前 20 行**：
   ```powershell
   curl http://127.0.0.1:7090/pac.js | Select-Object -First 20
   ```

4. **端口占用情况**：
   ```powershell
   netstat -ano | findstr ":7090"
   netstat -ano | findstr ":10808"
   ```

## 高级诊断

### 使用 PowerShell 测试 PAC 功能

```powershell
# 测试 PAC 服务器
$response = Invoke-WebRequest -Uri "http://127.0.0.1:7090/pac.js" -UseBasicParsing
Write-Host "PAC 文件大小：$($response.Content.Length) 字节"
Write-Host "代理配置：$($response.Content -match 'var proxy = (.+);' | Out-Null; $Matches[1])"

# 测试 SOCKS5 代理端口
Test-NetConnection -ComputerName 127.0.0.1 -Port 10808
```

### 手动测试代理

使用 curl 测试：
```powershell
# 通过 SOCKS5 代理访问 Google
curl --socks5 127.0.0.1:10808 https://www.google.com -v
```

## 联系支持

如果尝试所有方法后仍无法解决，请提供：
1. 完整的应用日志
2. 服务器配置（隐藏敏感信息）
3. 错误发生的详细步骤
4. Windows 版本和网络环境信息



