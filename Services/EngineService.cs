using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Windows.ApplicationModel;

namespace App2.Services;

/// <summary>
/// 负责启动和停止 sslocal.exe 进程
/// </summary>
public class EngineService : IDisposable
{
    private Process? _process;
    private readonly string _enginePath;

    public bool IsRunning => _process != null && !_process.HasExited;

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
    public void Start(string configPath)
    {
        if (IsRunning)
        {
            throw new InvalidOperationException("进程已在运行中");
        }

        if (!File.Exists(configPath))
        {
            throw new FileNotFoundException("配置文件不存在", configPath);
        }

        _process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = _enginePath,
                Arguments = $"-c \"{configPath}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = Path.GetDirectoryName(_enginePath)
            }
        };

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

        // 监听进程退出
        _process.Exited += (s, e) =>
        {
            LogReceived?.Invoke(this, $"[WARN] sslocal 进程已退出，退出代码: {_process.ExitCode}");
        };
        _process.EnableRaisingEvents = true;

        _process.Start();
        _process.BeginOutputReadLine();
        _process.BeginErrorReadLine();

        LogReceived?.Invoke(this, $"sslocal 已启动，PID: {_process.Id}");
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
