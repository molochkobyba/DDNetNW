namespace DDNetNW.Models;

public sealed record ClientSnapshot(
    string Nickname,
    string Score,
    string Team,
    bool IsAfk,
    bool IsPlayer,
    string Clan
);
