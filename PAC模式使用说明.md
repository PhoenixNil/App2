# PAC 模式使用说明

## 功能概述

PAC（Proxy Auto-Config）模式已完全集成到应用中。它使用本地 HTTP 服务器托管 PAC 脚本文件，使浏览器能够根据域名规则自动决定是否使用代理。

## 功能特性

1. **自动域名匹配**：基于 `pac.txt` 中的大量域名规则自动判断
2. **本地 PAC 服务器**：运行在 `http://127.0.0.1:1090/pac.js`
3. **智能切换**：在不同代理模式间切换时自动启动/停止 PAC 服务器

## 架构组件

### 1. PACServerService
- 位置：`Services/PACServerService.cs`
- 功能：
  - 托管本地 HTTP 服务器（端口 1090）
  - 读取 `pac.txt` 并替换代理地址占位符
  - 提供 PAC 文件给浏览器和系统

### 2. ProxyService 更新
- 位置：`Services/ProxyService.cs`
- 新增功能：
  - `SetPACUrl()` 方法设置 PAC 文件 URL
  - `EnablePACProxy()` 方法正确配置系统注册表

### 3. MainWindowViewModel 集成
- 位置：`ViewModels/MainWindowViewModel.cs`
- 集成点：
  - 启动服务时自动启动 PAC 服务器（仅在 PAC 模式）
  - 停止服务时自动停止 PAC 服务器
  - 切换代理模式时自动管理 PAC 服务器状态

## 使用方法

1. **启动应用**
   - 打开应用，默认选择 PAC 模式

2. **配置服务器**
   - 添加或选择一个 SS 服务器节点

3. **启动代理**
   - 点击"启动"按钮
   - PAC 服务器会自动在后台启动
   - 系统代理设置会自动配置

4. **验证 PAC 模式**
   - 打开浏览器，访问 `http://127.0.0.1:1090/pac.js`
   - 应该能看到完整的 PAC 脚本内容
   - 其中 `__PROXY__` 占位符已被替换为实际的代理地址

5. **测试效果**
   - 访问国内网站（如 baidu.com）：直连
   - 访问列表中的域名（如 google.com）：通过代理

## PAC 文件结构

pac.txt 文件包含：
```javascript
var proxy = '__PROXY__';  // 会被替换为 'SOCKS5 127.0.0.1:端口; SOCKS 127.0.0.1:端口'
var rules = [
    [
        [],
        ["域名列表..."]
    ]
];

function FindProxyForURL(url, host) {
    // 匹配逻辑
}
```

## 技术细节

### 代理格式
- PAC 文件中使用 SOCKS5 代理格式
- 示例：`SOCKS5 127.0.0.1:10808; SOCKS 127.0.0.1:10808`
- 支持 SOCKS5 和 SOCKS 回退

### 注册表配置
在 PAC 模式下，系统注册表配置：
```
[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Internet Settings]
"ProxyEnable" = 0
"AutoConfigURL" = "http://127.0.0.1:1090/pac.js"
```

### 端口使用
- Shadowsocks 本地端口：默认 10808（可配置）
- PAC HTTP 服务器端口：1090（固定）

## 模式切换

### 切换到 PAC 模式
1. 在运行时切换到 PAC 模式
2. PAC 服务器自动启动
3. 系统代理配置更新为 AutoConfigURL

### 切换到全局/直连模式
1. 在运行时切换到其他模式
2. PAC 服务器自动停止
3. 系统代理配置相应更新

## 故障排查

### PAC 文件无法访问
- 检查 pac.txt 是否存在于应用目录
- 检查端口 1090 是否被占用
- 查看应用日志获取详细错误

### 代理不生效
- 验证 Shadowsocks 服务是否正常运行
- 检查浏览器是否使用系统代理设置
- 确认访问的域名是否在 pac.txt 规则列表中

### 切换模式失败
- 查看调试输出（Debug.WriteLine）
- 确认没有其他程序锁定代理设置

## 维护和更新

### 更新域名规则
1. 编辑 `pac.txt` 文件
2. 在 `rules` 数组中添加/删除域名
3. 重新启动应用或重启代理服务

### 自定义 PAC 端口
修改 `MainWindowViewModel.cs` 中的端口：
```csharp
_pacServerService = new PACServerService(1090); // 修改这里
```

## 优势

1. **自动化**：无需手动配置浏览器代理
2. **智能**：根据域名自动决定是否使用代理
3. **高效**：国内网站直连，国外网站代理
4. **灵活**：支持动态切换代理模式

## 已知限制

1. PAC 服务器端口固定为 1090
2. 某些应用可能不支持 PAC（需要使用全局模式）
3. PAC 规则更新需要重启服务生效


