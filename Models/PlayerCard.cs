using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using WpfBrush = System.Windows.Media.Brush;

namespace DDNetNW.Models;

public sealed class PlayerCard : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private PlayerState _state = PlayerState.Unknown;
    private string _serverName = string.Empty;
    private string _serverAddress = string.Empty;
    private string _mapName = string.Empty;
    private string _gameType = string.Empty;
    private string _score = "not available";
    private string _team = "unknown";
    private bool _isAfk;
    private bool _isPlayer;
    private string _clan = string.Empty;
    private DateTime? _lastSeen;

    private PlayerCard(string nickname)
    {
        Nickname = nickname;
    }

    public string Nickname { get; }

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

    public string StatusDot => "●";

    public string StatusText => State switch
    {
        PlayerState.Online => "Online",
        PlayerState.Offline => "Offline",
        _ => "Waiting"
    };

    public string MainLine => State switch
    {
        PlayerState.Online => $"Map: {MapName}",
        PlayerState.Offline => "Offline",
        _ => "Waiting for first scan..."
    };

    public string DetailsLine1 => State switch
    {
        PlayerState.Online => $"Server: {ServerName}",
        PlayerState.Offline => LastSeen is null ? "No online session detected yet." : $"Last seen: {LastSeen:HH:mm:ss}",
        _ => "The app will update this card after the next scan."
    };

    public string DetailsLine2 => State == PlayerState.Online ? $"Address: {ServerAddress}" : string.Empty;

    public string DetailsLine3 => State == PlayerState.Online
        ? $"Score/Time: {Score} · Team: {Team} · AFK: {(IsAfk ? "yes" : "no")} · Type: {(IsPlayer ? "player" : "spectator")}"
        : string.Empty;

    public WpfBrush BorderBrush => State switch
    {
        PlayerState.Online => BrushFromHex("#3DFF9F"),
        PlayerState.Offline => BrushFromHex("#FF4F6D"),
        _ => BrushFromHex("#FAD66B")
    };

    public WpfBrush BadgeBackground => State switch
    {
        PlayerState.Online => BrushFromHex("#0C2A25"),
        PlayerState.Offline => BrushFromHex("#2A101A"),
        _ => BrushFromHex("#2A2410")
    };

    public WpfBrush BadgeForeground => State switch
    {
        PlayerState.Online => BrushFromHex("#B9FFD8"),
        PlayerState.Offline => BrushFromHex("#FFC2CC"),
        _ => BrushFromHex("#FFEAA3")
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

    private void RefreshCalculatedProperties()
    {
        OnPropertyChanged(nameof(StatusDot));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(MainLine));
        OnPropertyChanged(nameof(DetailsLine1));
        OnPropertyChanged(nameof(DetailsLine2));
        OnPropertyChanged(nameof(DetailsLine3));
        OnPropertyChanged(nameof(BorderBrush));
        OnPropertyChanged(nameof(BadgeBackground));
        OnPropertyChanged(nameof(BadgeForeground));
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
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
}
