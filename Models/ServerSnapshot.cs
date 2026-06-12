using System.Collections.Generic;

namespace DDNetNW.Models;

public sealed record ServerSnapshot(
    string ServerName,
    string ServerAddress,
    string Location,
    string MapName,
    string GameType,
    int PlayerCount,
    int MaxPlayers,
    IReadOnlyList<ClientSnapshot> Clients
);
