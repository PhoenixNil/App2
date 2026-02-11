using System;
using System.Collections.Generic;
using System.IO;
using Windows.ApplicationModel;

namespace App2.Services;

/// <summary>
/// 负责 ACL 文件定位、基础校验与规则统计。
/// </summary>
public class AclService : IAclService
{
	private readonly IEngineService _engineService;

	public AclService(IEngineService engineService)
	{
		_engineService = engineService;
	}

	public string? ResolveAclPath()
	{
		var candidates = new List<string>();

		void AddCandidate(string? path)
		{
			if (string.IsNullOrWhiteSpace(path))
			{
				return;
			}

			var fullPath = Path.GetFullPath(path);
			if (!candidates.Exists(item => item.Equals(fullPath, StringComparison.OrdinalIgnoreCase)))
			{
				candidates.Add(fullPath);
			}
		}

		AddCandidate(Path.Combine(AppContext.BaseDirectory, "shadowsocks.acl"));

		try
		{
			if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 19041))
			{
				AddCandidate(Path.Combine(Package.Current.InstalledPath, "shadowsocks.acl"));
			}
		}
		catch
		{
			// MSIX API 在非打包环境可能不可用，忽略并继续尝试其他路径
		}

		AddCandidate(Path.Combine(_engineService.EngineDirectory, "shadowsocks.acl"));
		AddCandidate(Path.Combine(_engineService.EngineDirectory, "..", "shadowsocks.acl"));
		AddCandidate(Path.Combine(_engineService.EngineDirectory, "..", "..", "shadowsocks.acl"));

		foreach (var candidate in candidates)
		{
			if (File.Exists(candidate))
			{
				return candidate;
			}
		}

		return null;
	}

	public bool ValidateAclFile(string aclPath, out string error)
	{
		try
		{
			if (string.IsNullOrWhiteSpace(aclPath))
			{
				error = "路径为空。";
				return false;
			}

			var fullPath = Path.GetFullPath(aclPath);
			if (!File.Exists(fullPath))
			{
				error = $"文件不存在: {fullPath}";
				return false;
			}

			using var stream = File.Open(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
			using var reader = new StreamReader(stream);
			var hasAclModeHeader = false;
			var scannedLines = 0;
			while (!reader.EndOfStream && scannedLines < 256)
			{
				var line = (reader.ReadLine() ?? string.Empty).Trim();
				line = line.TrimStart('\uFEFF');
				scannedLines++;
				if (line.Length == 0 || line.StartsWith('#'))
				{
					continue;
				}

				if (line.Equals("[proxy_all]", StringComparison.OrdinalIgnoreCase)
					|| line.Equals("[bypass_all]", StringComparison.OrdinalIgnoreCase))
				{
					hasAclModeHeader = true;
				}

				break;
			}

			if (!hasAclModeHeader)
			{
				error = "ACL 文件缺少 [proxy_all] 或 [bypass_all] 节。";
				return false;
			}

			error = string.Empty;
			return true;
		}
		catch (Exception ex)
		{
			error = $"无法读取 ACL 文件: {ex.Message}";
			return false;
		}
	}

	public bool TryGetAclRuleStats(string aclPath, out int ipRuleCount, out int domainRuleCount)
	{
		ipRuleCount = 0;
		domainRuleCount = 0;

		try
		{
			var inBypassList = false;
			foreach (var rawLine in File.ReadLines(aclPath))
			{
				var line = (rawLine ?? string.Empty).Trim();
				line = line.TrimStart('\uFEFF');
				if (line.Length == 0 || line.StartsWith('#'))
				{
					continue;
				}

				if (line.StartsWith('['))
				{
					inBypassList = line.Equals("[bypass_list]", StringComparison.OrdinalIgnoreCase);
					continue;
				}

				if (!inBypassList)
				{
					continue;
				}

				if (IsIpOrCidrRule(line))
				{
					ipRuleCount++;
				}
				else
				{
					domainRuleCount++;
				}
			}

			return true;
		}
		catch
		{
			return false;
		}
	}

	private static bool IsIpOrCidrRule(string line)
	{
		return line.Contains('/') || System.Net.IPAddress.TryParse(line, out _);
	}
}
