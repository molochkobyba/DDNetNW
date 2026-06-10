namespace DDNetNW.Models;

public sealed record PlayerScanResult(
    string Nickname,
    string ServerName,
    string ServerAddress,
    string MapName,
    string GameType,
    string Score,
    string Team,
    bool IsAfk,
    bool IsPlayer,
    string Clan
);
