using System;

namespace DDNetNW.Models;

public sealed class MapServerInfo
{
    public MapServerInfo(string serverName, string serverAddress, string mapName, string gameType, int playerCount, int maxPlayers)
    {
        ServerName = serverName;
        ServerAddress = serverAddress;
        MapName = mapName;
        GameType = gameType;
        PlayerCount = playerCount;
        MaxPlayers = maxPlayers;
    }

    public string ServerName { get; }
    public string ServerAddress { get; }
    public string MapName { get; }
    public string GameType { get; }
    public int PlayerCount { get; }
    public int MaxPlayers { get; }

    public string PlayersLine => MaxPlayers > 0 ? $"{PlayerCount}/{MaxPlayers} players" : $"{PlayerCount} players";
    public string DetailsLine => string.IsNullOrWhiteSpace(GameType) ? ServerAddress : $"{GameType} • {ServerAddress}";
    public string ConnectCommand => $"connect {NormalizeAddressForConnect(ServerAddress)}";

    private static string NormalizeAddressForConnect(string address)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            return string.Empty;
        }

        const string prefix = "tw-0.6+udp://";
        return address.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ? address[prefix.Length..] : address;
    }
}
