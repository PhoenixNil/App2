using System.Collections.Generic;
using App2.Models;

namespace App2.Services;

public interface IConfigStorage
{
	void SaveServers(IEnumerable<ServerEntry> servers);
	List<ServerEntry> LoadServers();
	void ClearAll();
}
