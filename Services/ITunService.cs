namespace App2.Services;

public interface ITunService
{
	string DefaultTunInterfaceName { get; }
	string DefaultTunInterfaceAddress { get; }
	string[] DnsServers { get; }
	string TunGateway { get; }

	string? DetectOutboundInterface(bool forceRefresh = false);
	bool IsWintunAvailable();
	string GetExpectedWintunPath();
	string[] GetAvailableInterfaces();
	string? GetDefaultGateway();
	int? GetTunInterfaceIndex(string tunInterfaceName = "shadowsocks-tun");
	bool SetupTunRoutes(string serverAddress);
	void CleanupTunRoutes(string? serverAddress);
}
