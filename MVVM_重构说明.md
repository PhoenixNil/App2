# MVVM 重构说明

## 概述

本次重构将 MainWindow.xaml.cs 从传统的 code-behind 模式重构为标准的 MVVM (Model-View-ViewModel) 架构模式。

## 重构内容

### 1. 新增文件

#### ViewModels 文件夹
- **ViewModelBase.cs** - ViewModel 基类，实现 INotifyPropertyChanged 接口
- **RelayCommand.cs** - 命令实现类，支持同步命令和带参数的命令
- **AsyncRelayCommand.cs** - 异步命令实现类，用于处理异步操作
- **MainWindowViewModel.cs** - 主窗口的 ViewModel，包含所有业务逻辑

#### Models 文件夹
- **ServerEntry.cs** - 从 MainWindow.xaml.cs 中迁移出来的服务器条目模型

### 2. 修改文件

#### MainWindow.xaml
- 添加了 `viewmodels` 和 `models` 命名空间
- 将 UI 元素的绑定从 code-behind 改为使用 `{x:Bind ViewModel.*}` 绑定到 ViewModel
- 移除了 `ServersListView_SelectionChanged` 事件，改用双向绑定 `SelectedItem`
- 移除了 `ColorComboBox_SelectionChanged` 事件，改用双向绑定 `SelectedIndex`
- 部分按钮改用 Command 绑定（如 `BtnTestLatency`）

主要绑定更改：
```xml
<!-- 服务器列表 -->
ItemsSource="{x:Bind ViewModel.Servers, Mode=OneWay}"
SelectedItem="{x:Bind ViewModel.SelectedServer, Mode=TwoWay}"

<!-- 服务器详情 -->
Text="{x:Bind ViewModel.SelectedName, Mode=OneWay}"
Text="{x:Bind ViewModel.SelectedHost, Mode=OneWay}"
Text="{x:Bind ViewModel.SelectedPort, Mode=OneWay}"
Text="{x:Bind ViewModel.SelectedMethod, Mode=OneWay}"

<!-- 延迟测试 -->
Text="{x:Bind ViewModel.LatencyText, Mode=OneWay}"
Foreground="{x:Bind ViewModel.LatencyForeground, Mode=OneWay}"
Command="{x:Bind ViewModel.TestLatencyCommand}"

<!-- 状态 -->
Text="{x:Bind ViewModel.StatusText, Mode=OneWay}"
Foreground="{x:Bind ViewModel.StatusIconForeground, Mode=OneWay}"

<!-- 代理模式 -->
SelectedIndex="{x:Bind ViewModel.ProxyModeIndex, Mode=TwoWay}"
```

#### MainWindow.xaml.cs
- 大幅简化，从 1159 行减少到约 500 行
- 移除所有业务逻辑，只保留 View 相关的代码
- 添加 `ViewModel` 属性，用于数据绑定
- 保留 UI 对话框相关的代码（因为这些涉及 UI 元素创建）
- 保留主题管理和窗口初始化代码

主要保留的内容：
- 窗口初始化和设置（DPI、大小、标题栏等）
- ContentDialog 创建和显示（导入 SS、添加/编辑/删除服务器、查看日志等）
- 主题切换相关的 UI 代码
- ViewModel 的初始化和清理

### 3. 依赖关系更新

#### Services 文件夹
- **ConfigStorage.cs** - 已使用 `App2.Models` 命名空间
- **ConfigWriter.cs** - 已使用 `App2.Models` 命名空间

## MVVM 架构优势

### 1. 关注点分离
- **Model (Models/ServerEntry.cs)**: 数据模型
- **View (MainWindow.xaml)**: UI 展示
- **ViewModel (MainWindowViewModel.cs)**: 业务逻辑和状态管理

### 2. 可测试性
- ViewModel 不依赖于 UI 组件，可以独立进行单元测试
- 业务逻辑与 UI 逻辑分离，便于测试

### 3. 可维护性
- 代码结构清晰，职责明确
- MainWindow.xaml.cs 大幅简化，只关注 View 层的逻辑
- 业务逻辑集中在 ViewModel 中，便于维护和修改

### 4. 数据绑定
- 使用双向绑定自动同步 UI 和数据
- 减少手动更新 UI 的代码
- 通过 INotifyPropertyChanged 实现自动 UI 更新

### 5. 命令模式
- 使用 ICommand 接口统一处理用户操作
- 支持 CanExecute 逻辑，自动管理控件的启用/禁用状态
- AsyncRelayCommand 优雅地处理异步操作

## ViewModel 主要功能

### 属性 (Properties)
- `Servers` - 服务器列表（ObservableCollection）
- `SelectedServer` - 当前选中的服务器
- `StatusText` - 状态文本
- `StatusIconForeground` - 状态图标颜色
- `LatencyText` / `LatencyForeground` - 延迟显示
- `LocalPortText` - 本地端口
- `StartStopButtonContent` / `StartStopButtonChecked` - 启动/停止按钮状态
- `CanEditServer` / `CanRemoveServer` - 按钮启用状态
- `ProxyModeIndex` - 代理模式索引

### 命令 (Commands)
- `StartStopCommand` - 启动/停止代理
- `TestLatencyCommand` - 测试延迟

### 公开方法 (Public Methods)
- `Initialize()` - ViewModel 初始化
- `Cleanup()` - 清理资源
- `ParseSSUrl()` - 解析 SS 链接
- `CreateServerEntry()` - 创建服务器条目
- `AddServer()` - 添加服务器
- `UpdateServer()` - 更新服务器
- `RemoveServer()` - 移除服务器
- `ValidateAndUpdateLocalPort()` - 验证并更新本地端口
- `GetLogsText()` - 获取日志文本
- `ApplyTheme()` - 应用主题

## View 保留功能

MainWindow.xaml.cs 保留了以下 View 层的职责：

### 1. UI 对话框
- 导入 SS 链接对话框
- 添加/编辑服务器对话框
- 删除确认对话框
- 查看日志对话框
- 编辑本地端口对话框

这些对话框涉及复杂的 UI 元素创建和交互，在 MVVM 模式中通常仍在 View 层处理。

### 2. 主题管理
- 主题切换逻辑（涉及 FrameworkElement 的直接操作）
- 主题按钮状态更新

### 3. 窗口初始化
- DPI 缩放计算
- 窗口大小设置
- 标题栏设置

## 编译结果

✅ 编译成功，无错误，无警告

```
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

## 使用建议

### 1. 添加新功能
- 在 ViewModel 中添加属性和命令
- 在 XAML 中绑定到这些属性和命令
- 只在 View 中处理纯 UI 相关的逻辑

### 2. 测试
- 可以独立创建 MainWindowViewModel 实例进行单元测试
- 不需要创建 UI 即可测试业务逻辑

### 3. 扩展
- 如需添加新的 ViewModel，继承 ViewModelBase 类
- 使用 RelayCommand 或 AsyncRelayCommand 实现命令
- 通过 SetProperty 方法自动触发属性更改通知

## 注意事项

1. **x:Bind 与 Binding 的区别**
   - 本项目使用 `x:Bind`，它是编译时绑定，性能更好
   - `x:Bind` 默认是 OneTime 模式，需要显式指定 Mode=OneWay 或 TwoWay

2. **命令参数**
   - 某些 UI 操作仍通过事件处理，因为需要访问 ContentDialog 等 UI 元素
   - 可以考虑使用消息传递机制进一步解耦（如 MVVM Toolkit 的 Messenger）

3. **资源清理**
   - ViewModel 的 Cleanup() 方法在窗口关闭时调用
   - 确保所有服务和资源被正确释放

## 后续优化建议

1. **使用 MVVM Toolkit**
   - 考虑引入 CommunityToolkit.Mvvm NuGet 包
   - 使用 `[ObservableProperty]` 和 `[RelayCommand]` 特性简化代码

2. **依赖注入**
   - 使用 DI 容器管理 ViewModel 和 Service 的生命周期
   - 便于测试和解耦

3. **导航服务**
   - 如果应用扩展到多页面，考虑实现导航服务
   - 在 ViewModel 中通过服务进行页面导航，而不是直接操作 UI

4. **对话框服务**
   - 创建 IDialogService 接口封装对话框显示
   - ViewModel 通过服务请求显示对话框，进一步解耦 View 和 ViewModel

5. **单元测试**
   - 为 ViewModel 编写单元测试
   - 测试业务逻辑、命令执行、属性更改等

