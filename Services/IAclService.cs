namespace App2.Services;

/// <summary>
/// ACL 文件解析与校验服务。
/// </summary>
public interface IAclService
{
	string? ResolveAclPath();
	bool ValidateAclFile(string aclPath, out string error);
	bool TryGetAclRuleStats(string aclPath, out int ipRuleCount, out int domainRuleCount);
}
