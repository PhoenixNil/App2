# Shadowsocks WinUI 3 客户端 / Shadowsocks WinUI 3 Client

一个基于 WinUI 3 现代化 Shadowsocks Windows 客户端，提供简洁优雅的用户界面。
![1762515836660.png](https://youke1.picui.cn/s1/2025/11/07/690ddbf6ad03e.png)
![1762515975040.png](https://youke1.picui.cn/s1/2025/11/07/690ddbf7050e4.png)

## ✨ Features

- 🚀 **现代化界面**：基于 WinUI 3 的 Fluent Design 设计语言
- 🔧 **服务器管理**：添加、编辑、删除服务器，支持 SS URL 导入
- 🌐 **多种代理模式**：全局模式、PAC 自动配置模式、直连模式
- 📊 **延迟测试**：服务器延迟测试，支持自动重试
- 🛣️ **路由模式**：支持绕过中国大陆（ACL）和全局路由
- 💾 **配置持久化**：自动保存配置，支持导入导出
- 🎨 **主题切换**：支持浅色、深色和跟随系统主题
- 🔌 **系统托盘**：最小化到托盘，快速访问常用功能
- 📡 **内置 PAC 服务器**：自动代理配置服务
- 🎯 **拖拽排序**：支持服务器列表拖拽重新排序


## 🛠️ 技术栈 / Tech Stack

- **.NET 8.0**
- **Windows App SDK (WinUI 3)**
- **MVVM Architecture**
- **C# + XAML**
- **Shadowsocks-rust (sslocal.exe)**
- **CommunityToolkit.Mvvm**

## 📋 系统要求 / System Requirements

### 中文
- Windows 10 版本 19041 或更高版本
- Windows 11（推荐）
- .NET 8.0 Runtime
- x64 架构

### English
- Windows 10 version 19041 or higher
- Windows 11 (Recommended)
- .NET 8.0 Runtime
- x64 architecture

## 📦 安装 / Installation

### 中文

1. **克隆仓库**
```bash
git clone <repository-url>
cd App2
```

2. **还原依赖**
```bash
dotnet restore
```

3. **编译项目**
```bash
dotnet build
```

4. **运行应用**
```bash
dotnet run
```

### English

1. **Clone the repository**
```bash
git clone <repository-url>
cd App2
```

2. **Restore dependencies**
```bash
dotnet restore
```

3. **Build the project**
```bash
dotnet build
```

4. **Run the application**
```bash
dotnet run
```



## ⚙️ 配置说明 / Configuration


配置文件保存在：`%APPDATA%\App2\config.json`

配置结构：
```json
{
  "Servers": [],
  "LocalPort": 1080,
  "ProxyMode": "Direct",
  "RouteMode": "BypassChinaWithACL"
}
```



## 🔧 开发 / Development


**构建要求**
- Visual Studio 2022 或更高版本
- Windows App SDK 1.6 或更高版本
- .NET 8.0 SDK

**调试**
```bash
dotnet run --configuration Debug
```

**发布**
```bash
dotnet publish -c Release -r win-x64 --self-contained
```


## 🤝 贡献 / Contributing

欢迎提交 Issue 和 Pull Request！

1. Fork 本仓库
2. 创建特性分支 (`git checkout -b feature/AmazingFeature`)
3. 提交更改 (`git commit -m 'Add some AmazingFeature'`)
4. 推送到分支 (`git push origin feature/AmazingFeature`)
5. 开启 Pull Request

### English
Issues and Pull Requests are welcome!

1. Fork the repository
2. Create your feature branch (`git checkout -b feature/AmazingFeature`)
3. Commit your changes (`git commit -m 'Add some AmazingFeature'`)
4. Push to the branch (`git push origin feature/AmazingFeature`)
5. Open a Pull Request

## 📄 许可证 / License

本项目采用 MIT 许可证 - 详见 [LICENSE](LICENSE) 文件

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details

## 📮 联系方式 / Contact

如有问题或建议，请提交 Issue。

For questions or suggestions, please submit an Issue.

---

**注意 / Note**: 本项目仅供学习和研究使用，请遵守当地法律法规。/ This project is for educational and research purposes only. Please comply with local laws and regulations.
