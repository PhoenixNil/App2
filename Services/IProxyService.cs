namespace App2.Services;

public interface IProxyService
{
	void SetProxyServer(string host, int port);
	void SetProxyMode(ProxyMode mode);
	ProxyMode GetCurrentMode();
	void SetPACUrl(string pacUrl);
	void ClearProxy();
}
