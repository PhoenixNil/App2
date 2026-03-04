using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Security.Principal;
using Windows.ApplicationModel;

namespace App2.Services;

/// <summary>
/// 负责启动和停止 sslocal.exe 进程
/// </summary>
public class EngineService : IEngineService
{
    private Process? _process;
    private readonly string _enginePath;

    public bool IsRunning => _process != null && !_process.HasExited;
    public string EnginePath => _enginePath;
    public string EngineDirectory => Path.GetDirectoryName(_enginePath) ?? AppContext.BaseDirectory;

    public event EventHandler<string>? LogReceived;

    public EngineService()
    {
        // 尝试多个可能的路径
        var possiblePaths = new List<string>();

        // MSIX 打包后的路径（仅在支持的版本上尝试）
        try
        {
            if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 19041))
            {
                possiblePaths.Add(Path.Combine(Package.Current.InstalledPath, "Assets", "engine", "sslocal.exe"));
            }
        }
        catch
        {
            // 忽略，继续尝试其他路径
        }

        // 开发环境路径（bin/Debug 或 bin/Release）
        possiblePaths.Add(Path.Combine(AppContext.BaseDirectory, "Assets", "engine", "sslocal.exe"));
        // 直接在 bin 目录下
        possiblePaths.Add(Path.Combine(AppContext.BaseDirectory, "sslocal.exe"));

        _enginePath = string.Empty;

        foreach (var path in possiblePaths)
        {
            if (File.Exists(path))
            {
                _enginePath = path;
                break;
            }
        }

        if (string.IsNullOrEmpty(_enginePath))
        {
            throw new FileNotFoundException("找不到 sslocal.exe 引擎文件");
        }
    }

    /// <summary>
    /// 启动 sslocal 进程
    /// </summary>
    /// <param name="configPath">配置文件路径</param>
    /// <param name="isTunMode">是否为 TUN 模式</param>
    /// <param name="outboundInterface">出站网络接口（TUN 模式必需）</param>
    /// <param name="tunInterfaceName">TUN 接口名称</param>
    /// <param name="tunInterfaceAddress">TUN 接口地址（CIDR 格式，如 10.255.0.1/24）</param>
    public void Start(
        string configPath,
        bool isTunMode = false,
        string? outboundInterface = null,
        string? tunInterfaceName = null,
        string? tunInterfaceAddress = null,
        bool requireAdmin = false)
    {
        if (IsRunning)
        {
            throw new InvalidOperationException("进程已在运行中");
        }

        if (!File.Exists(configPath))
        {
            throw new FileNotFoundException("配置文件不存在", configPath);
        }

        // 构建命令行参数
        var arguments = $"-c \"{configPath}\"";

        if (isTunMode)
        {
            arguments += " --protocol tun";

            if (!string.IsNullOrEmpty(outboundInterface))
            {
                arguments += $" --outbound-bind-interface \"{outboundInterface}\"";
            }

            if (!string.IsNullOrEmpty(tunInterfaceName))
            {
                arguments += $" --tun-interface-name \"{tunInterfaceName}\"";
            }

            // TUN 接口地址是正确配置路由的关键参数
            if (!string.IsNullOrEmpty(tunInterfaceAddress))
            {
                arguments += $" --tun-interface-address \"{tunInterfaceAddress}\"";
            }
        }

        var runElevated = requireAdmin && !IsAdministrator();

        LogReceived?.Invoke(this, $"配置文件: {configPath}");
        LogReceived?.Invoke(this, $"工作目录: {EngineDirectory}");
        LogReceived?.Invoke(this, $"启动命令: {_enginePath} {arguments}");
        if (runElevated)
        {
            LogReceived?.Invoke(this, "TUN 模式需要管理员权限，正在请求提升...");
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = _enginePath,
            Arguments = arguments,
            WorkingDirectory = Path.GetDirectoryName(_enginePath)
        };

        if (runElevated)
        {
            startInfo.UseShellExecute = true;
            startInfo.Verb = "runas";
            startInfo.WindowStyle = ProcessWindowStyle.Hidden;
        }
        else
        {
            startInfo.UseShellExecute = false;
            startInfo.CreateNoWindow = true;
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardError = true;
        }

        _process = new Process { StartInfo = startInfo };

        // 监听进程退出
        _process.Exited += (s, e) =>
        {
            LogReceived?.Invoke(this, $"[WARN] sslocal 进程已退出，退出代码: {_process.ExitCode}");
        };
        _process.EnableRaisingEvents = true;

        try
        {
            _process.Start();
        }
        catch (Win32Exception ex) when (runElevated && ex.NativeErrorCode == 1223)
        {
            _process.Dispose();
            _process = null;
            throw new InvalidOperationException("已取消管理员授权，无法启动 TUN 模式。", ex);
        }

        if (!runElevated)
        {
            _process.OutputDataReceived += (s, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    LogReceived?.Invoke(this, $"[INFO] {e.Data}");
                }
            };

            _process.ErrorDataReceived += (s, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    // 过滤常见的非关键错误
                    if (e.Data.Contains("connection closed before message completed"))
                    {
                        // 这是正常现象，不记录
                        return;
                    }
                    LogReceived?.Invoke(this, $"[ERROR] {e.Data}");
                }
            };

            _process.BeginOutputReadLine();
            _process.BeginErrorReadLine();
        }

        LogReceived?.Invoke(this, $"sslocal 已启动，PID: {_process.Id}");
    }

    private static bool IsAdministrator()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 停止 sslocal 进程
    /// </summary>
    public void Stop()
    {
        if (_process == null || _process.HasExited)
        {
            return;
        }

        try
        {
            _process.Kill(true);
            _process.WaitForExit(3000);
            LogReceived?.Invoke(this, "sslocal 已停止");
        }
        catch (Exception ex)
        {
            LogReceived?.Invoke(this, $"停止进程时出错: {ex.Message}");
        }
        finally
        {
            _process?.Dispose();
            _process = null;
        }
    }

    public void Dispose()
    {
        Stop();
    }
}
