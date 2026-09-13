namespace ServerListPlayersFix.Configuration;

public class ServerListPlayersFixConfig
{
    public bool Enabled { get; set; } = true;
    public float UpdateIntervalSeconds { get; set; } = 5.0f;
    public bool ForceHeartbeatOnUpdate { get; set; } = true;
    public bool DebugLog { get; set; } = false;
}
