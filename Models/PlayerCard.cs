using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using DDNetNW.Services;

namespace DDNetNW.Models;

public sealed class PlayerCard : INotifyPropertyChanged
{
    private PlayerState _state = PlayerState.Unknown;
    private string _nickname;
    private string _serverName = string.Empty;
    private string _serverAddress = string.Empty;
    private string _mapName = string.Empty;
    private string _gameType = string.Empty;
    private string _score = string.Empty;
    private string _team = string.Empty;
    private bool _isAfk;
    private bool _isPlayer;
    private string _clan = string.Empty;
    private DateTime? _lastSeen;
    private bool _isExpanded;
    private bool _showServerAddress;
    private bool _showExtraDetails;

    private PlayerCard(string nickname)
    {
        _nickname = nickname;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Nickname
    {
        get => _nickname;
        private set => SetField(ref _nickname, value);
    }

    public PlayerState State
    {
        get => _state;
        private set => SetField(ref _state, value);
    }

    public string ServerName
    {
        get => _serverName;
        private set => SetField(ref _serverName, value);
    }

    public string ServerAddress
    {
        get => _serverAddress;
        private set => SetField(ref _serverAddress, value);
    }

    public string MapName
    {
        get => _mapName;
        private set => SetField(ref _mapName, value);
    }

    public string GameType
    {
        get => _gameType;
        private set => SetField(ref _gameType, value);
    }

    public string Score
    {
        get => _score;
        private set => SetField(ref _score, value);
    }

    public string Team
    {
        get => _team;
        private set => SetField(ref _team, value);
    }

    public bool IsAfk
    {
        get => _isAfk;
        private set => SetField(ref _isAfk, value);
    }

    public bool IsPlayer
    {
        get => _isPlayer;
        private set => SetField(ref _isPlayer, value);
    }

    public string Clan
    {
        get => _clan;
        private set => SetField(ref _clan, value);
    }

    public DateTime? LastSeen
    {
        get => _lastSeen;
        private set => SetField(ref _lastSeen, value);
    }

    public bool IsExpanded
    {
        get => _isExpanded;
        private set => SetField(ref _isExpanded, value);
    }

    public string StatusText => State switch
    {
        PlayerState.Online => LocalizationService.Get("online"),
        PlayerState.Offline => LocalizationService.Get("offline"),
        _ => LocalizationService.Get("waiting")
    };

    public string SummaryLine => State switch
    {
        PlayerState.Online => $"{ServerName} • {MapName}",
        PlayerState.Offline => LocalizationService.Get("offline"),
        _ => LocalizationService.Get("waitingScan")
    };

    public string SecondaryLine => State switch
    {
        PlayerState.Online => string.IsNullOrWhiteSpace(Score) ? GameType : GameType,
        PlayerState.Offline => LastSeen is null ? LocalizationService.Get("noSession") : $"{LocalizationService.Get("lastSeen")}: {LastSeen:HH:mm:ss}",
        _ => LocalizationService.Get("waitingScan")
    };

    public string DetailsLine => State switch
    {
        PlayerState.Online => $"{LocalizationService.Get("server")}: {ServerName} • {LocalizationService.Get("map")}: {MapName}",
        PlayerState.Offline => LastSeen is null
            ? LocalizationService.Get("noSession")
            : $"{LocalizationService.Get("status")}: {LocalizationService.Get("offline")} • {LocalizationService.Get("lastSeen")}: {LastSeen:HH:mm:ss}",
        _ => LocalizationService.Get("waitingScan")
    };

    public string AddressLine => string.IsNullOrWhiteSpace(ServerAddress)
        ? $"{LocalizationService.Get("address")}: —"
        : $"{LocalizationService.Get("address")}: {ServerAddress}";

    public string ExtraDetailsLine
    {
        get
        {
            var clanValue = string.IsNullOrWhiteSpace(Clan) ? "—" : Clan;
            return $"{LocalizationService.Get("mode")}: {GameType} • {LocalizationService.Get("clan")}: {clanValue} • {LocalizationService.Get("team")}: {Team} • {LocalizationService.Get("afk")}: {(IsAfk ? LocalizationService.Get("yes") : LocalizationService.Get("no"))} • {(IsPlayer ? LocalizationService.Get("player") : LocalizationService.Get("spectator"))}";
        }
    }

    public Visibility ExpandedVisibility => IsExpanded ? Visibility.Visible : Visibility.Collapsed;
    public Visibility AddressVisibility => IsExpanded && State == PlayerState.Online && _showServerAddress ? Visibility.Visible : Visibility.Collapsed;
    public Visibility ExtraVisibility => IsExpanded && State == PlayerState.Online && _showExtraDetails ? Visibility.Visible : Visibility.Collapsed;

    public Brush BorderBrush => State switch
    {
        PlayerState.Online => GetResourceBrush("StateOnlineBorder", "#52D89A"),
        PlayerState.Offline => GetResourceBrush("StateOfflineBorder", "#F27A92"),
        _ => GetResourceBrush("StateWaitingBorder", "#D4A94B")
    };

    public Brush BadgeBackground => State switch
    {
        PlayerState.Online => GetResourceBrush("StateOnlineBadgeBackground", "#103225"),
        PlayerState.Offline => GetResourceBrush("StateOfflineBadgeBackground", "#34111B"),
        _ => GetResourceBrush("StateWaitingBadgeBackground", "#342A12")
    };

    public Brush BadgeForeground => State switch
    {
        PlayerState.Online => GetResourceBrush("StateOnlineBadgeForeground", "#C6FFE2"),
        PlayerState.Offline => GetResourceBrush("StateOfflineBadgeForeground", "#FFD2DA"),
        _ => GetResourceBrush("StateWaitingBadgeForeground", "#FFE9B4")
    };

    public static PlayerCard CreateUnknown(string nickname) => new(nickname);

    public void SetOnline(PlayerScanResult result)
    {
        State = PlayerState.Online;
        ServerName = result.ServerName;
        ServerAddress = result.ServerAddress;
        MapName = result.MapName;
        GameType = result.GameType;
        Score = result.Score;
        Team = result.Team;
        IsAfk = result.IsAfk;
        IsPlayer = result.IsPlayer;
        Clan = result.Clan;
        LastSeen = DateTime.Now;
        RefreshCalculatedProperties();
    }

    public void SetOffline()
    {
        State = PlayerState.Offline;
        RefreshCalculatedProperties();
    }

    public void Rename(string nickname)
    {
        Nickname = nickname;
        State = PlayerState.Unknown;
        ServerName = string.Empty;
        ServerAddress = string.Empty;
        MapName = string.Empty;
        GameType = string.Empty;
        Score = string.Empty;
        Team = string.Empty;
        IsAfk = false;
        IsPlayer = false;
        Clan = string.Empty;
        RefreshCalculatedProperties();
    }

    public void ToggleExpanded()
    {
        IsExpanded = !IsExpanded;
        RefreshCalculatedProperties();
    }

    public void UpdateDisplaySettings(bool showServerAddress, bool showExtraDetails)
    {
        _showServerAddress = showServerAddress;
        _showExtraDetails = showExtraDetails;
        RefreshCalculatedProperties();
    }

    public void RefreshLocalizedProperties() => RefreshCalculatedProperties();

    private void RefreshCalculatedProperties()
    {
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(SummaryLine));
        OnPropertyChanged(nameof(SecondaryLine));
        OnPropertyChanged(nameof(DetailsLine));
        OnPropertyChanged(nameof(AddressLine));
        OnPropertyChanged(nameof(ExtraDetailsLine));
        OnPropertyChanged(nameof(ExpandedVisibility));
        OnPropertyChanged(nameof(AddressVisibility));
        OnPropertyChanged(nameof(ExtraVisibility));
        OnPropertyChanged(nameof(BorderBrush));
        OnPropertyChanged(nameof(BadgeBackground));
        OnPropertyChanged(nameof(BadgeForeground));
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

    private static SolidColorBrush BrushFromHex(string hex)
    {
        return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
    }

    private static Brush GetResourceBrush(string key, string fallbackHex)
    {
        if (Application.Current?.Resources[key] is Brush brush)
        {
            return brush;
        }

        return BrushFromHex(fallbackHex);
    }
}
