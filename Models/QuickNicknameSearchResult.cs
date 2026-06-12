namespace DDNetNW.Models;

public sealed record QuickNicknameSearchResult(
    string RequestedNickname,
    bool IsOnline,
    PlayerScanResult? Player
)
{
    public string DisplayNickname => Player?.Nickname ?? RequestedNickname;
    public string Clan => Player?.Clan ?? string.Empty;
    public string MapName => Player?.MapName ?? string.Empty;
}
