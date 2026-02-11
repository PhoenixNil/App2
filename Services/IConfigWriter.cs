using App2.Models;

namespace App2.Services;

public interface IConfigWriter
{
	void WriteConfig(ServerEntry server, int localPort = 1080, string? aclPath = null, bool isTunMode = false, string[]? dnsServers = null);
	string GetConfigPath();
	void DeleteConfig();
}
