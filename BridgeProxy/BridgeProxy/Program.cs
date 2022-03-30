using BridgeProxy;
using System.Text.Json;

Console.WriteLine("Commands:");
Console.WriteLine("exit - закрытие программы");
Console.WriteLine();

var settingsFileName = args.ElementAtOrDefault(0) ?? "proxyconfig.json";
Console.WriteLine($"Init proxies from {settingsFileName}");
var proxies = JsonSerializer.Deserialize<ProxySettingsJson[]>(await File.ReadAllTextAsync(settingsFileName));
foreach (var item in proxies)
{
    new BridgeProxy.BridgeProxy(item).Start();
}

Console.WriteLine();
Console.WriteLine("Enter command");
string command;
while ((command = Console.ReadLine()) != "exit")
{

}