using System.Globalization;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using DDNetNW.Models;

namespace DDNetNW.Services;

public sealed class DdnetMasterClient : IDisposable
{
    private static readonly string[] MasterUrls =
    {
        "https://master1.ddnet.org/ddnet/15/servers.json",
        "https://master2.ddnet.org/ddnet/15/servers.json",
        "https://master3.ddnet.org/ddnet/15/servers.json",
        "https://master4.ddnet.org/ddnet/15/servers.json"
    };

    private readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(12) };

    public async Task<Dictionary<string, PlayerScanResult>> FindTrackedPlayersAsync(IEnumerable<string> nicknames)
    {
        using var document = await FetchServerListAsync();
        return ParseTrackedPlayers(document.RootElement, nicknames);
    }

    private async Task<JsonDocument> FetchServerListAsync()
    {
        Exception? lastError = null;

        foreach (var url in MasterUrls)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.UserAgent.ParseAdd(AppMetadata.UserAgent);

                using var response = await _httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                return JsonDocument.Parse(json);
            }
            catch (Exception ex)
            {
                lastError = ex;
            }
        }

        throw new InvalidOperationException($"Could not read DDNet master list. {lastError?.Message}");
    }

    private static Dictionary<string, PlayerScanResult> ParseTrackedPlayers(JsonElement root, IEnumerable<string> nicknames)
    {
        var targets = nicknames
            .Select(NormalizeName)
            .Where(name => name.Length > 0)
            .ToHashSet(StringComparer.Ordinal);

        var found = new Dictionary<string, PlayerScanResult>(StringComparer.Ordinal);

        if (!root.TryGetProperty("servers", out var servers) || servers.ValueKind != JsonValueKind.Array)
        {
            return found;
        }

        foreach (var server in servers.EnumerateArray())
        {
            if (!server.TryGetProperty("info", out var info) || info.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            if (!info.TryGetProperty("clients", out var clients) || clients.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            var serverName = ReadString(info, "name", "Unknown server");
            var gameType = ReadString(info, "game_type", "unknown");
            var mapName = ReadMapName(info);
            var address = ReadFirstAddress(server);

            foreach (var client in clients.EnumerateArray())
            {
                var nickname = ReadString(client, "name", string.Empty).Trim();
                var normalized = NormalizeName(nickname);

                if (!targets.Contains(normalized) || found.ContainsKey(normalized))
                {
                    continue;
                }

                found[normalized] = new PlayerScanResult(
                    Nickname: nickname,
                    ServerName: serverName,
                    ServerAddress: address,
                    MapName: mapName,
                    GameType: gameType,
                    Score: ReadScore(client),
                    Team: ReadValueAsText(client, "team", "unknown"),
                    IsAfk: ReadBool(client, "afk"),
                    IsPlayer: ReadBool(client, "is_player"),
                    Clan: ReadString(client, "clan", string.Empty)
                );
            }
        }

        return found;
    }

    public static string NormalizeName(string value)
    {
        return value.Normalize(NormalizationForm.FormC).Trim();
    }

    private static string ReadString(JsonElement obj, string propertyName, string fallback)
    {
        if (obj.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String)
        {
            return value.GetString() ?? fallback;
        }

        return fallback;
    }

    private static string ReadValueAsText(JsonElement obj, string propertyName, string fallback)
    {
        if (!obj.TryGetProperty(propertyName, out var value))
        {
            return fallback;
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString() ?? fallback,
            JsonValueKind.Number => value.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => fallback
        };
    }

    private static bool ReadBool(JsonElement obj, string propertyName)
    {
        if (!obj.TryGetProperty(propertyName, out var value))
        {
            return false;
        }

        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Number => value.TryGetInt32(out var number) && number != 0,
            _ => false
        };
    }

    private static string ReadScore(JsonElement client)
    {
        if (!client.TryGetProperty("score", out var score))
        {
            return "not available";
        }

        return score.ValueKind switch
        {
            JsonValueKind.Number => score.GetRawText(),
            JsonValueKind.String => score.GetString() ?? "not available",
            _ => "not available"
        };
    }

    private static string ReadMapName(JsonElement info)
    {
        if (!info.TryGetProperty("map", out var map))
        {
            return "Unknown map";
        }

        if (map.ValueKind == JsonValueKind.Object &&
            map.TryGetProperty("name", out var name) &&
            name.ValueKind == JsonValueKind.String)
        {
            return name.GetString() ?? "Unknown map";
        }

        if (map.ValueKind == JsonValueKind.String)
        {
            return map.GetString() ?? "Unknown map";
        }

        return "Unknown map";
    }

    private static string ReadFirstAddress(JsonElement server)
    {
        if (!server.TryGetProperty("addresses", out var addresses) || addresses.ValueKind != JsonValueKind.Array)
        {
            return "unknown address";
        }

        foreach (var address in addresses.EnumerateArray())
        {
            if (address.ValueKind == JsonValueKind.String)
            {
                return address.GetString() ?? "unknown address";
            }
        }

        return "unknown address";
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}
