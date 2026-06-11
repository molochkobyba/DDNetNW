using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace DDNetNW.Models;

public sealed class WatchedMapCard : INotifyPropertyChanged
{
    private string _name;
    private int _serversFound;
    private int _playersTotal;
    private string _bestServerLine = "No active servers";
    private DateTime? _lastUpdated;
    private bool _alertActive;

    private WatchedMapCard(string name)
    {
        _name = name;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Name
    {
        get => _name;
        private set => SetField(ref _name, value);
    }

    public int ServersFound
    {
        get => _serversFound;
        private set => SetField(ref _serversFound, value);
    }

    public int PlayersTotal
    {
        get => _playersTotal;
        private set => SetField(ref _playersTotal, value);
    }

    public string BestServerLine
    {
        get => _bestServerLine;
        private set => SetField(ref _bestServerLine, value);
    }

    public DateTime? LastUpdated
    {
        get => _lastUpdated;
        private set => SetField(ref _lastUpdated, value);
    }

    public bool AlertActive
    {
        get => _alertActive;
        set => SetField(ref _alertActive, value);
    }

    public ObservableCollection<MapServerInfo> Servers { get; } = new();

    public string ServersFoundLine => ServersFound == 1 ? "1 server found" : $"{ServersFound} servers found";
    public string PlayersTotalLine => PlayersTotal == 1 ? "1 player total" : $"{PlayersTotal} players total";
    public string LastUpdatedLine => LastUpdated is null ? "Not scanned yet" : $"Updated: {LastUpdated:HH:mm:ss}";

    public static WatchedMapCard Create(string name) => new(name);

    public void Rename(string name)
    {
        Name = name;
        AlertActive = false;
        RefreshCalculatedProperties();
    }

    public void UpdateServers(IEnumerable<MapServerInfo> servers)
    {
        Servers.Clear();

        foreach (var server in servers.OrderByDescending(server => server.PlayerCount).ThenBy(server => server.ServerName, StringComparer.OrdinalIgnoreCase))
        {
            Servers.Add(server);
        }

        ServersFound = Servers.Count;
        PlayersTotal = Servers.Sum(server => server.PlayerCount);
        BestServerLine = Servers.Count == 0 ? "No active servers" : $"Best: {Servers[0].ServerName} • {Servers[0].PlayerCount} players";
        LastUpdated = DateTime.Now;
        RefreshCalculatedProperties();
    }

    public void RefreshCalculatedProperties()
    {
        OnPropertyChanged(nameof(ServersFoundLine));
        OnPropertyChanged(nameof(PlayersTotalLine));
        OnPropertyChanged(nameof(LastUpdatedLine));
        OnPropertyChanged(nameof(BestServerLine));
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
