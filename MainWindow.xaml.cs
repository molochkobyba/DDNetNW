using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using DDNetNW.Models;
using DDNetNW.Services;

namespace DDNetNW;

/// <summary>
/// Main desktop window for DDNetNW.
/// Originally created by molochko.
/// </summary>
public partial class MainWindow : Window
{
    private readonly AppSettingsService _settingsService = new();
    private readonly DdnetMasterClient _masterClient = new();
    private readonly DispatcherTimer _scanTimer = new();

    private bool _scanInProgress;
    private bool _suppressSettingsSave;
    private bool _windowLoaded;
    private int _cooldownSeconds = 30;

    public ObservableCollection<PlayerCard> Players { get; } = new();
    public ObservableCollection<string> Events { get; } = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;

        ConfigureApplicationText();
        LoadSettingsIntoUi();
        RegisterUiEvents();
        ConfigureScanTimer();
    }

    private void ConfigureApplicationText()
    {
        Title = $"{AppMetadata.DisplayName} {AppMetadata.Version}";
    }

    private void RegisterUiEvents()
    {
        Players.CollectionChanged += (_, _) =>
        {
            UpdateStaticUi();
            SaveSettings();
        };

        WindowsNotificationsCheck.Checked += (_, _) => SaveSettings();
        WindowsNotificationsCheck.Unchecked += (_, _) => SaveSettings();

        Loaded += async (_, _) =>
        {
            _windowLoaded = true;
            UpdateStaticUi();
            AddEvent($"Application started. Settings path: {_settingsService.SettingsFilePath}");

            if (Players.Count > 0)
            {
                await ScanAsync();
            }
        };
    }

    private void ConfigureScanTimer()
    {
        _scanTimer.Interval = TimeSpan.FromSeconds(_cooldownSeconds);
        _scanTimer.Tick += async (_, _) => await ScanAsync();
        _scanTimer.Start();
    }

    private void Navigation_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is not RadioButton radio || radio.Tag is not string page)
        {
            return;
        }

        if (HomePage is null || NotificationsPage is null || AboutPage is null || SettingsPage is null || PageTitle is null || PageSubtitle is null)
        {
            return;
        }

        HomePage.Visibility = page == "Home" ? Visibility.Visible : Visibility.Collapsed;
        NotificationsPage.Visibility = page == "Notifications" ? Visibility.Visible : Visibility.Collapsed;
        AboutPage.Visibility = page == "About" ? Visibility.Visible : Visibility.Collapsed;
        SettingsPage.Visibility = page == "Settings" ? Visibility.Visible : Visibility.Collapsed;

        PageTitle.Text = page switch
        {
            "Notifications" => "Notifications",
            "About" => "About",
            "Settings" => "Settings",
            _ => "Main menu"
        };

        PageSubtitle.Text = page switch
        {
            "Notifications" => "Choose where status changes should be sent.",
            "About" => "Application details and DDNet server list usage.",
            "Settings" => "Adjust scan interval and local behavior.",
            _ => "Track DDNet nicknames from the public server list."
        };
    }

    private async void AddNickname_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new AddNicknameWindow { Owner = this };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        var nickname = dialog.Nickname.Trim();
        var normalized = DdnetMasterClient.NormalizeName(nickname);

        if (Players.Any(player => DdnetMasterClient.NormalizeName(player.Nickname) == normalized))
        {
            AddEvent($"Nickname already exists: {nickname}");
            return;
        }

        Players.Add(PlayerCard.CreateUnknown(nickname));
        AddEvent($"Added nickname: {nickname}");
        SaveSettings();
        await ScanAsync();
    }

    private void RemoveNickname_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not string nickname)
        {
            return;
        }

        var item = Players.FirstOrDefault(player => player.Nickname == nickname);
        if (item is null)
        {
            return;
        }

        Players.Remove(item);
        AddEvent($"Removed nickname: {nickname}");
        SaveSettings();
    }

    private async void ScanNow_Click(object sender, RoutedEventArgs e)
    {
        await ScanAsync();
    }

    private void CooldownSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        _cooldownSeconds = Math.Max(5, (int)Math.Round(e.NewValue));

        if (CooldownValueText is not null)
        {
            CooldownValueText.Text = FormatSeconds(_cooldownSeconds);
        }

        if (FooterIntervalText is not null)
        {
            FooterIntervalText.Text = $"Check every: {FormatSeconds(_cooldownSeconds)}";
        }

        if (_scanTimer is not null)
        {
            _scanTimer.Interval = TimeSpan.FromSeconds(_cooldownSeconds);
        }

        SaveSettings();
    }

    private void LoadSettingsIntoUi()
    {
        _suppressSettingsSave = true;

        try
        {
            var settings = _settingsService.Load();
            _cooldownSeconds = Math.Clamp(settings.CooldownSeconds, 5, 180);

            if (CooldownSlider is not null)
            {
                CooldownSlider.Value = _cooldownSeconds;
            }

            if (CooldownValueText is not null)
            {
                CooldownValueText.Text = FormatSeconds(_cooldownSeconds);
            }

            if (FooterIntervalText is not null)
            {
                FooterIntervalText.Text = $"Check every: {FormatSeconds(_cooldownSeconds)}";
            }

            if (WindowsNotificationsCheck is not null)
            {
                WindowsNotificationsCheck.IsChecked = settings.WindowsNotificationsEnabled;
            }

            foreach (var nickname in settings.Nicknames
                         .Where(name => !string.IsNullOrWhiteSpace(name))
                         .Select(name => name.Trim())
                         .Distinct(StringComparer.Ordinal))
            {
                Players.Add(PlayerCard.CreateUnknown(nickname));
            }
        }
        catch (Exception ex)
        {
            AddEvent($"Settings load error: {ex.Message}");
        }
        finally
        {
            _suppressSettingsSave = false;
        }
    }

    private void SaveSettings()
    {
        if (_suppressSettingsSave || !_windowLoaded)
        {
            return;
        }

        try
        {
            var settings = new AppSettings
            {
                CooldownSeconds = _cooldownSeconds,
                WindowsNotificationsEnabled = WindowsNotificationsCheck?.IsChecked == true,
                Nicknames = Players.Select(player => player.Nickname).ToList()
            };

            _settingsService.Save(settings);
        }
        catch (Exception ex)
        {
            AddEvent($"Settings save error: {ex.Message}");
        }
    }

    private async Task ScanAsync()
    {
        if (_scanInProgress || Players.Count == 0)
        {
            return;
        }

        _scanInProgress = true;
        SetScanningState();

        try
        {
            var results = await _masterClient.FindTrackedPlayersAsync(Players.Select(player => player.Nickname));

            foreach (var player in Players)
            {
                var key = DdnetMasterClient.NormalizeName(player.Nickname);
                results.TryGetValue(key, out var scanResult);
                ApplyScanResult(player, scanResult);
            }

            SetScanSuccessState();
        }
        catch (Exception ex)
        {
            SetScanErrorState(ex.Message);
        }
        finally
        {
            _scanInProgress = false;
            UpdateStaticUi();
        }
    }

    private void ApplyScanResult(PlayerCard player, PlayerScanResult? result)
    {
        if (result is null)
        {
            if (player.State == PlayerState.Online)
            {
                AddEvent($"{player.Nickname} left");
                ShowLocalNotification($"{player.Nickname} is offline", "The nickname is no longer visible in the DDNet server list.");
            }

            player.SetOffline();
            return;
        }

        if (player.State != PlayerState.Online)
        {
            AddEvent($"{player.Nickname} joined {result.ServerName} / {result.MapName}");
            ShowLocalNotification($"{player.Nickname} is online", $"{result.ServerName}\n{result.MapName}");
        }
        else if (!string.Equals(player.ServerAddress, result.ServerAddress, StringComparison.Ordinal))
        {
            AddEvent($"{player.Nickname} changed server: {result.ServerName} / {result.MapName}");
            ShowLocalNotification($"{player.Nickname} changed server", $"{result.ServerName}\n{result.MapName}");
        }
        else if (!string.Equals(player.MapName, result.MapName, StringComparison.Ordinal))
        {
            AddEvent($"{player.Nickname} changed map: {player.MapName} → {result.MapName}");
            ShowLocalNotification($"{player.Nickname} changed map", $"{player.MapName} → {result.MapName}");
        }

        player.SetOnline(result);
    }

    private void SetScanningState()
    {
        SidebarStatusText.Text = "Scanning...";
        SidebarStatusText.Foreground = BrushFromHex("#FAD66B");
        TopStatusText.Text = "● Scanning";
        TopStatusText.Foreground = BrushFromHex("#FAD66B");
    }

    private void SetScanSuccessState()
    {
        var now = DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
        FooterLastScanText.Text = $"Last scan: {now}";
        SidebarScanText.Text = $"Last scan: {now}";
        FooterMasterText.Text = "master status: OK";
        SidebarStatusText.Text = "Monitoring active";
        SidebarStatusText.Foreground = BrushFromHex("#3DFF9F");
        TopStatusText.Text = "● Monitoring ON";
        TopStatusText.Foreground = BrushFromHex("#B9FFD8");
    }

    private void SetScanErrorState(string message)
    {
        FooterMasterText.Text = "master status: error";
        SidebarStatusText.Text = "Connection error";
        SidebarStatusText.Foreground = BrushFromHex("#FF4F6D");
        TopStatusText.Text = "● Connection error";
        TopStatusText.Foreground = BrushFromHex("#FF8FA2");
        AddEvent($"Scan error: {message}");
    }

    private void AddEvent(string text)
    {
        var line = $"{DateTime.Now:HH:mm:ss}  {text}";
        Events.Insert(0, line);

        while (Events.Count > 6)
        {
            Events.RemoveAt(Events.Count - 1);
        }
    }

    private void ShowLocalNotification(string title, string message)
    {
        if (WindowsNotificationsCheck?.IsChecked != true)
        {
            return;
        }

        try
        {
            System.Media.SystemSounds.Asterisk.Play();
        }
        catch
        {
            // Local notification failures must not interrupt monitoring.
        }

        AddEvent($"Notification: {title} — {message.Replace("\n", " / ")}");
    }

    private void UpdateStaticUi()
    {
        EmptyState.Visibility = Players.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        FooterPlayersText.Text = Players.Count == 1 ? "1 nickname" : $"{Players.Count} nicknames";
        FooterIntervalText.Text = $"Check every: {FormatSeconds(_cooldownSeconds)}";
    }

    private static string FormatSeconds(int seconds)
    {
        return seconds == 1 ? "1 second" : $"{seconds} seconds";
    }

    private static SolidColorBrush BrushFromHex(string hex)
    {
        return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
    }

    protected override void OnClosed(EventArgs e)
    {
        SaveSettings();
        _scanTimer.Stop();
        _masterClient.Dispose();
        base.OnClosed(e);
    }
}
