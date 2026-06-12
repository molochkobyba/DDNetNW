using System;
using System.Linq;
using System.Threading.Tasks;
using DDNetNW.Models;

namespace DDNetNW.Services;

/// <summary>
/// Small isolated quick-search block.
/// UI can call this service without knowing how DDNet master data is fetched or scanned.
/// </summary>
public sealed class NicknameSearchService
{
    private readonly DdnetMasterClient _masterClient;

    public NicknameSearchService(DdnetMasterClient masterClient)
    {
        _masterClient = masterClient;
    }

    public async Task<QuickNicknameSearchResult> SearchExactAsync(string nickname)
    {
        var requested = nickname.Trim();

        if (string.IsNullOrWhiteSpace(requested))
        {
            return new QuickNicknameSearchResult(string.Empty, false, null);
        }

        var snapshot = await _masterClient.ReadSnapshotAsync();
        var found = DdnetMasterClient.FindTrackedPlayers(snapshot, new[] { requested });
        found.TryGetValue(DdnetMasterClient.NormalizeName(requested), out var player);

        return new QuickNicknameSearchResult(requested, player is not null, player);
    }
}
