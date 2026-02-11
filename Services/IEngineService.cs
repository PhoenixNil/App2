using System;

namespace App2.Services;

public interface IEngineService : IDisposable
{
	bool IsRunning { get; }
	string EnginePath { get; }
	string EngineDirectory { get; }

	event EventHandler<string>? LogReceived;

	void Start(
		string configPath,
		bool isTunMode = false,
		string? outboundInterface = null,
		string? tunInterfaceName = null,
		string? tunInterfaceAddress = null,
		bool requireAdmin = false);

	void Stop();
}
