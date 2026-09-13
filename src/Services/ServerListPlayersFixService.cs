using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ServerListPlayersFix.Configuration;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Commands;
using SwiftlyS2.Shared.Events;
using SwiftlyS2.Shared.Players;

namespace ServerListPlayersFix.Services;

public class ServerListPlayersFixService
{
    private readonly ISwiftlyCore Core;
    private readonly IOptionsMonitor<ServerListPlayersFixConfig> _config;
    private ServerListPlayersFixConfig? _fallbackConfig;
    private CancellationTokenSource? _updateCts;

    public ServerListPlayersFixService(ISwiftlyCore core, IOptionsMonitor<ServerListPlayersFixConfig> config)
    {
        Core = core;
        _config = config;
        core.Registrator.Register(this);

        _config.OnChange(_ => StartUpdateLoop());
        StartUpdateLoop();
    }

    public ServerListPlayersFixConfig GetActiveConfig()
    {
        var current = _config.CurrentValue;
        if (current != null) return current;

        if (_fallbackConfig == null)
        {
            _fallbackConfig = LoadConfigFromFile();
        }

        return _fallbackConfig ?? new ServerListPlayersFixConfig();
    }

    private ServerListPlayersFixConfig LoadConfigFromFile()
    {
        try
        {
            string[] possiblePaths = new[]
            {
                Path.Combine(Directory.GetCurrentDirectory(), "configs", "plugins", "ServerListPlayersFix", "config.jsonc"),
                Path.Combine(Directory.GetCurrentDirectory(), "configs", "ServerListPlayersFix", "config.jsonc"),
                Path.Combine(Core.PluginPath, "configs", "config.jsonc"),
                Path.Combine(Core.PluginPath, "resources", "config.jsonc"),
                Path.Combine(Core.PluginPath, "config.jsonc"),
            };

            foreach (var path in possiblePaths)
            {
                if (File.Exists(path))
                {
                    string json = File.ReadAllText(path);
                    var options = new JsonSerializerOptions
                    {
                        ReadCommentHandling = JsonCommentHandling.Skip,
                        AllowTrailingCommas = true,
                        PropertyNameCaseInsensitive = true
                    };

                    var cfg = JsonSerializer.Deserialize<ServerListPlayersFixConfig>(json, options);
                    if (cfg != null) return cfg;
                }
            }
        }
        catch (Exception ex)
        {
            Core.Logger.LogError(ex, "[ServerListPlayersFix] Error reading config directly from file.");
        }

        return new ServerListPlayersFixConfig();
    }

    public void StartUpdateLoop()
    {
        StopUpdateLoop();

        var config = GetActiveConfig();
        if (!config.Enabled) return;

        float intervalSec = config.UpdateIntervalSeconds > 0 ? config.UpdateIntervalSeconds : 5.0f;
        int delayMs = (int)(intervalSec * 1000);

        _updateCts = new CancellationTokenSource();
        var token = _updateCts.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    await Task.Delay(delayMs, token);
                    if (token.IsCancellationRequested) break;

                    Core.Scheduler.NextTick(PerformPlayerUpdate);
                }
            }
            catch (TaskCanceledException) { }
            catch (Exception ex)
            {
                Core.Logger.LogError(ex, "[ServerListPlayersFix] Error in update loop.");
            }
        }, token);

        Core.Logger.LogInformation($"[ServerListPlayersFix] Started Steam API player list sync (Interval: {intervalSec}s).");
    }

    public void StopUpdateLoop()
    {
        if (_updateCts != null)
        {
            _updateCts.Cancel();
            _updateCts.Dispose();
            _updateCts = null;
        }
    }

    private void PerformPlayerUpdate()
    {
        var config = GetActiveConfig();
        if (!config.Enabled) return;

        var validPlayers = Core.PlayerManager.GetAllValidPlayers()
            .Where(p => p != null && p.IsValid && !p.IsFakeClient && p.SteamID > 0)
            .ToList();

        if (config.ForceHeartbeatOnUpdate)
        {
            Core.Engine.ExecuteCommand("heartbeat");
        }

        if (config.DebugLog)
        {
            Core.Logger.LogInformation($"[ServerListPlayersFix] Synced user data for {validPlayers.Count} connected player(s) to Steam Master API.");
        }
    }

    [EventListener<EventDelegates.OnClientConnected>]
    public void OnClientConnected(IOnClientConnectedEvent e)
    {
        var config = GetActiveConfig();
        if (!config.Enabled) return;

        Core.Scheduler.DelayBySeconds(1.0f, () => PerformPlayerUpdate());
    }

    [EventListener<EventDelegates.OnClientDisconnected>]
    public void OnClientDisconnected(IOnClientDisconnectedEvent e)
    {
        var config = GetActiveConfig();
        if (!config.Enabled) return;

        Core.Scheduler.DelayBySeconds(1.0f, () => PerformPlayerUpdate());
    }

    [Command("serverlistplayersfix_reload", permission: "@admin/root")]
    [CommandAlias("sw_serverlistplayersfix_reload")]
    public void Command_Reload(ICommandContext context)
    {
        _fallbackConfig = null;
        StartUpdateLoop();

        string msg = "[LIME][ServerListPlayersFix][DEFAULT] Конфигурация успешно перезагружена!";
        if (context.IsSentByPlayer && context.Sender != null)
        {
            context.Sender.SendChat(msg);
        }
        else
        {
            context.Reply("[ServerListPlayersFix] Configuration successfully reloaded!");
        }
    }
}
