using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ServerListPlayersFix.Configuration;
using ServerListPlayersFix.Services;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Plugins;

namespace ServerListPlayersFix;

public partial class ServerListPlayersFixPlugin : BasePlugin
{
    private ServiceProvider? _serviceProvider;
    private ServerListPlayersFixService? _service;

    public ServerListPlayersFixPlugin(ISwiftlyCore core) : base(core)
    {
    }

    public override void Load(bool hotReload)
    {
        Core.Configuration
            .InitializeJsonWithModel<ServerListPlayersFixConfig>("config.jsonc", "ServerListPlayersFix")
            .Configure(builder =>
            {
                string pluginDir = Core.PluginPath;
                string baseDir = Directory.GetCurrentDirectory();

                string[] paths = new[]
                {
                    Path.Combine(baseDir, "configs", "plugins", "ServerListPlayersFix", "config.jsonc"),
                    Path.Combine(baseDir, "configs", "ServerListPlayersFix", "config.jsonc"),
                    Path.Combine(pluginDir, "configs", "config.jsonc"),
                    Path.Combine(pluginDir, "resources", "config.jsonc"),
                    Path.Combine(pluginDir, "config.jsonc")
                };

                string? validPath = paths.FirstOrDefault(File.Exists);
                if (validPath != null)
                {
                    builder.AddJsonFile(validPath, optional: false, reloadOnChange: true);
                }
                else
                {
                    builder.AddJsonFile("config.jsonc", optional: true, reloadOnChange: true);
                }
            });

        ServiceCollection services = new();

        services
            .AddSwiftly(Core)
            .AddSingleton<ServerListPlayersFixService>()
            .AddOptionsWithValidateOnStart<ServerListPlayersFixConfig>()
            .BindConfiguration("ServerListPlayersFix");

        _serviceProvider = services.BuildServiceProvider();
        _service = _serviceProvider.GetRequiredService<ServerListPlayersFixService>();
    }

    public override void Unload()
    {
    }
}
