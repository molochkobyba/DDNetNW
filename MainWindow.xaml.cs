using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using DDNetNW.Models;
using DDNetNW.Services;

namespace DDNetNW;

public partial class MainWindow : Window
{
    private readonly AppSettingsService _settingsService = new();
    private readonly DdnetMasterClient _masterClient = new();
    private readonly LocalNotificationService _notificationService = new();
    private readonly DispatcherTimer _scanTimer = new();

    private bool _scanInProgress;
    private bool _suppressSettingsSave;
    private bool _windowLoaded;
    private int _cooldownSeconds = 30;
    private int _mapAlertMinPlayers = 8;
    private string _currentLanguage = "en";
    private string _currentTheme = "dark";
    private string _homeMode = "Players";
    private string _mapServerFilter = "Any";
    private bool _showServerAddress;
    private bool _showExtraDetails;
    private WatchedMapCard? _selectedMap;

    public ObservableCollection<PlayerCard> Players { get; } = new();
    public ObservableCollection<WatchedMapCard> WatchedMaps { get; } = new();
    public ObservableCollection<MapServerInfo> SelectedMapServers { get; } = new();
    public ObservableCollection<string> Events { get; } = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;

        ConfigureApplicationText();
        RegisterUiEvents();
        LoadSettingsIntoUi();
        ApplyTheme(_currentTheme);
        ApplyLocalization();
        ApplyCardDisplaySettings();
        SetHomeMode(_homeMode);
        ConfigureScanTimer();
    }

    private void ConfigureApplicationText()
    {
        Title = $"{AppMetadata.DisplayName} {AppMetadata.Version}";
        WindowTitleText.Text = Title;
        SidebarVersionText.Text = AppMetadata.Version;
        AboutVersionText.Text = $"Version: {AppMetadata.Version}";
        AboutCreatorText.Text = AppMetadata.CreatorLine;
    }

    private void RegisterUiEvents()
    {
        Players.CollectionChanged += (_, _) =>
        {
            UpdateStaticUi();
            SaveSettings();
        };

        WatchedMaps.CollectionChanged += (_, _) =>
        {
            UpdateStaticUi();
            SaveSettings();
        };

        Loaded += async (_, _) =>
        {
            _windowLoaded = true;
            UpdateStaticUi();
            AddEvent(_currentLanguage == "ru" ? "Приложение запущено." : "Application started.");

            if (Players.Count > 0 || WatchedMaps.Count > 0)
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

        if (HomePage is null || NotificationsPage is null || AboutPage is null || OptionsPage is null)
        {
            return;
        }

        HomePage.Visibility = page == "Home" ? Visibility.Visible : Visibility.Collapsed;
        NotificationsPage.Visibility = page == "Notifications" ? Visibility.Visible : Visibility.Collapsed;
        AboutPage.Visibility = page == "About" ? Visibility.Visible : Visibility.Collapsed;
        OptionsPage.Visibility = page == "Options" ? Visibility.Visible : Visibility.Collapsed;
    }

    private void HomeMode_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is not RadioButton radio || radio.Tag is not string mode)
        {
            return;
        }

        SetHomeMode(mode);
    }

    private void SetHomeMode(string mode)
    {
        _homeMode = mode;

        if (PlayersPanel is null || MapsOverviewPanel is null || MapDetailsPanel is null)
        {
            return;
        }

        var mapsMode = string.Equals(mode, "Maps", StringComparison.OrdinalIgnoreCase);

        PlayersPanel.Visibility = mapsMode ? Visibility.Collapsed : Visibility.Visible;
        MapsOverviewPanel.Visibility = mapsMode && _selectedMap is null ? Visibility.Visible : Visibility.Collapsed;
        MapDetailsPanel.Visibility = mapsMode && _selectedMap is not null ? Visibility.Visible : Visibility.Collapsed;
        AddNicknameButton.Visibility = mapsMode ? Visibility.Collapsed : Visibility.Visible;
        AddMapButton.Visibility = mapsMode ? Visibility.Visible : Visibility.Collapsed;

        if (PlayersModeButton is not null && MapsModeButton is not null)
        {
            PlayersModeButton.IsChecked = !mapsMode;
            MapsModeButton.IsChecked = mapsMode;
        }

        UpdateStaticUi();
    }

    private async void AddNickname_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new AddNicknameWindow { Owner = this, LanguageCode = _currentLanguage, IsEditMode = false, IsMapMode = false };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        var nickname = dialog.Nickname.Trim();
        var normalized = DdnetMasterClient.NormalizeName(nickname);

        if (Players.Any(player => DdnetMasterClient.NormalizeName(player.Nickname) == normalized))
        {
            ShowDuplicateMessage(_currentLanguage == "ru" ? "Такой ник уже есть в списке." : "This nickname is already in the list.");
            AddEvent(_currentLanguage == "ru" ? $"Ник уже добавлен: {nickname}" : $"Nickname already exists: {nickname}");
            return;
        }

        var card = PlayerCard.CreateUnknown(nickname);
        card.UpdateDisplaySettings(_showServerAddress, _showExtraDetails);
        Players.Add(card);
        AddEvent(_currentLanguage == "ru" ? $"Добавлен ник: {nickname}" : $"Added nickname: {nickname}");
        SaveSettings();
        await ScanAsync();
    }

    private async void AddMap_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new AddNicknameWindow { Owner = this, LanguageCode = _currentLanguage, IsEditMode = false, IsMapMode = true };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        var mapName = dialog.Nickname.Trim();
        var normalized = DdnetMasterClient.NormalizeMapName(mapName);

        if (WatchedMaps.Any(map => string.Equals(DdnetMasterClient.NormalizeMapName(map.Name), normalized, StringComparison.OrdinalIgnoreCase)))
        {
            ShowDuplicateMessage(_currentLanguage == "ru" ? "Такая карта уже есть в списке." : "This map is already in the list.");
            AddEvent(_currentLanguage == "ru" ? $"Карта уже добавлена: {mapName}" : $"Map already exists: {mapName}");
            return;
        }

        WatchedMaps.Add(WatchedMapCard.Create(mapName));
        AddEvent(_currentLanguage == "ru" ? $"Добавлена карта: {mapName}" : $"Added map: {mapName}");
        SaveSettings();
        SetHomeMode("Maps");
        await ScanAsync();
    }

    private async void EditNickname_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not PlayerCard card)
        {
            return;
        }

        var dialog = new AddNicknameWindow { Owner = this, LanguageCode = _currentLanguage, IsEditMode = true, IsMapMode = false, InitialNickname = card.Nickname };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        var newNickname = dialog.Nickname.Trim();
        var normalized = DdnetMasterClient.NormalizeName(newNickname);

        if (Players.Any(player => !ReferenceEquals(player, card) && DdnetMasterClient.NormalizeName(player.Nickname) == normalized))
        {
            ShowDuplicateMessage(_currentLanguage == "ru" ? "Такой ник уже есть в списке." : "This nickname is already in the list.");
            AddEvent(_currentLanguage == "ru" ? $"Ник уже добавлен: {newNickname}" : $"Nickname already exists: {newNickname}");
            return;
        }

        var oldNickname = card.Nickname;
        card.Rename(newNickname);
        card.UpdateDisplaySettings(_showServerAddress, _showExtraDetails);
        AddEvent(_currentLanguage == "ru" ? $"Ник изменён: {oldNickname} → {newNickname}" : $"Nickname updated: {oldNickname} → {newNickname}");
        SaveSettings();
        await ScanAsync();
    }

    private async void EditMap_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;

        if (sender is not Button button || button.Tag is not WatchedMapCard map)
        {
            return;
        }

        var dialog = new AddNicknameWindow { Owner = this, LanguageCode = _currentLanguage, IsEditMode = true, IsMapMode = true, InitialNickname = map.Name };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        var newMapName = dialog.Nickname.Trim();
        var normalized = DdnetMasterClient.NormalizeMapName(newMapName);

        if (WatchedMaps.Any(existing => !ReferenceEquals(existing, map) && string.Equals(DdnetMasterClient.NormalizeMapName(existing.Name), normalized, StringComparison.OrdinalIgnoreCase)))
        {
            ShowDuplicateMessage(_currentLanguage == "ru" ? "Такая карта уже есть в списке." : "This map is already in the list.");
            AddEvent(_currentLanguage == "ru" ? $"Карта уже добавлена: {newMapName}" : $"Map already exists: {newMapName}");
            return;
        }

        var oldName = map.Name;
        map.Rename(newMapName);
        AddEvent(_currentLanguage == "ru" ? $"Карта изменена: {oldName} → {newMapName}" : $"Map updated: {oldName} → {newMapName}");
        SaveSettings();
        await ScanAsync();
    }

    private void RemoveNickname_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not PlayerCard card)
        {
            return;
        }

        Players.Remove(card);
        AddEvent(_currentLanguage == "ru" ? $"Удалён ник: {card.Nickname}" : $"Removed nickname: {card.Nickname}");
        SaveSettings();
    }

    private void RemoveMap_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;

        if (sender is not Button button || button.Tag is not WatchedMapCard map)
        {
            return;
        }

        WatchedMaps.Remove(map);
        AddEvent(_currentLanguage == "ru" ? $"Удалена карта: {map.Name}" : $"Removed map: {map.Name}");

        if (ReferenceEquals(_selectedMap, map))
        {
            _selectedMap = null;
            SelectedMapServers.Clear();
            SetHomeMode("Maps");
        }

        SaveSettings();
    }

    private void ToggleDetails_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not PlayerCard card)
        {
            return;
        }

        card.ToggleExpanded();
    }

    private void OpenMap_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Border border || border.Tag is not WatchedMapCard map)
        {
            return;
        }

        _selectedMap = map;
        RefreshSelectedMapDetails();
        SetHomeMode("Maps");
    }

    private void BackToMaps_Click(object sender, RoutedEventArgs e)
    {
        _selectedMap = null;
        SelectedMapServers.Clear();
        SetHomeMode("Maps");
    }

    private void CopyServerAddress_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not MapServerInfo server)
        {
            return;
        }

        Clipboard.SetText(server.ServerAddress);
        AddEvent(_currentLanguage == "ru" ? "Адрес сервера скопирован." : "Server address copied.");
    }

    private void CopyConnectCommand_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not MapServerInfo server)
        {
            return;
        }

        Clipboard.SetText(server.ConnectCommand);
        AddEvent(_currentLanguage == "ru" ? "Команда подключения скопирована." : "Connect command copied.");
    }

    private async void ScanNow_Click(object sender, RoutedEventArgs e)
    {
        await ScanAsync();
    }

    private void ToggleNotificationOptions_Click(object sender, RoutedEventArgs e)
    {
        NotificationOptionsPanel.Visibility = NotificationOptionsPanel.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
    }

    private void CooldownSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (CooldownValueText is null || FooterIntervalText is null)
        {
            return;
        }

        _cooldownSeconds = Math.Max(5, (int)Math.Round(e.NewValue));
        CooldownValueText.Text = _currentLanguage == "ru" ? $"{_cooldownSeconds} с" : $"{_cooldownSeconds} s";
        FooterIntervalText.Text = _currentLanguage == "ru" ? $"Проверка: {FormatSeconds(_cooldownSeconds)}" : $"Check every: {FormatSeconds(_cooldownSeconds)}";
        _scanTimer.Interval = TimeSpan.FromSeconds(_cooldownSeconds);
        SaveSettings();
    }

    private void MapAlertPlayersSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (MapAlertPlayersValueText is null)
        {
            return;
        }

        _mapAlertMinPlayers = Math.Clamp((int)Math.Round(e.NewValue), 0, 64);
        MapAlertPlayersValueText.Text = _mapAlertMinPlayers.ToString(CultureInfo.InvariantCulture);
        SaveSettings();
    }

    private void LanguageSegment_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is not RadioButton button || button.Tag is not string language)
        {
            return;
        }

        _currentLanguage = language;
        LocalizationService.CurrentLanguage = _currentLanguage;

        if (HomeHeaderText is not null)
        {
            ApplyLocalization();
        }

        SaveSettings();
    }

    private void ThemeSegment_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is not RadioButton button || button.Tag is not string theme)
        {
            return;
        }

        _currentTheme = theme;
        ApplyTheme(_currentTheme);
        SaveSettings();
    }

    private void MapServerFilter_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is not RadioButton button || button.Tag is not string filter)
        {
            return;
        }

        _mapServerFilter = filter;
        SaveSettings();
        _ = ScanAsync();
    }

    private void NotificationOptionChanged(object sender, RoutedEventArgs e)
    {
        SaveSettings();
    }

    private void DisplayOptionChanged(object sender, RoutedEventArgs e)
    {
        if (ShowAddressCheck is null || ShowExtraDetailsCheck is null)
        {
            return;
        }

        _showServerAddress = ShowAddressCheck.IsChecked == true;
        _showExtraDetails = ShowExtraDetailsCheck.IsChecked == true;
        ApplyCardDisplaySettings();
        SaveSettings();
    }

    private void LoadSettingsIntoUi()
    {
        _suppressSettingsSave = true;

        try
        {
            var settings = _settingsService.Load();
            _cooldownSeconds = Math.Clamp(settings.CooldownSeconds, 5, 180);
            _mapAlertMinPlayers = Math.Clamp(settings.MapAlertMinPlayers, 0, 64);
            _mapServerFilter = string.IsNullOrWhiteSpace(settings.MapServerFilter) ? "Any" : settings.MapServerFilter;
            _currentLanguage = string.IsNullOrWhiteSpace(settings.Language) ? "en" : settings.Language;
            _currentTheme = string.IsNullOrWhiteSpace(settings.Theme) ? "dark" : settings.Theme;
            _showServerAddress = settings.ShowServerAddress;
            _showExtraDetails = settings.ShowExtraPlayerDetails;

            LocalizationService.CurrentLanguage = _currentLanguage;

            CooldownSlider.Value = _cooldownSeconds;
            CooldownValueText.Text = _currentLanguage == "ru" ? $"{_cooldownSeconds} с" : $"{_cooldownSeconds} s";
            MapAlertPlayersSlider.Value = _mapAlertMinPlayers;
            MapAlertPlayersValueText.Text = _mapAlertMinPlayers.ToString(CultureInfo.InvariantCulture);

            WindowsNotificationsCheck.IsChecked = settings.WindowsNotificationsEnabled;
            NotifyJoinCheck.IsChecked = settings.NotifyOnJoin;
            NotifyLeaveCheck.IsChecked = settings.NotifyOnLeave;
            NotifyServerCheck.IsChecked = settings.NotifyOnServerChange;
            NotifyMapCheck.IsChecked = settings.NotifyOnMapChange;
            MapAlertsCheck.IsChecked = settings.MapAlertsEnabled;
            PlaySoundCheck.IsChecked = settings.PlayNotificationSound;
            ShowAddressCheck.IsChecked = settings.ShowServerAddress;
            ShowExtraDetailsCheck.IsChecked = settings.ShowExtraPlayerDetails;

            SelectRadioByTag(new[] { LanguageEnglishButton, LanguageRussianButton }, _currentLanguage);
            SelectRadioByTag(new[] { ThemeLightButton, ThemeDarkButton }, _currentTheme);
            SelectRadioByTag(new[] { FilterAnyButton, FilterGerButton, FilterRusButton, FilterGerRusButton }, _mapServerFilter);

            foreach (var nickname in settings.Nicknames.Where(name => !string.IsNullOrWhiteSpace(name)).Select(name => name.Trim()).Distinct(StringComparer.Ordinal))
            {
                var card = PlayerCard.CreateUnknown(nickname);
                card.UpdateDisplaySettings(_showServerAddress, _showExtraDetails);
                Players.Add(card);
            }

            foreach (var mapName in settings.WatchedMaps.Where(name => !string.IsNullOrWhiteSpace(name)).Select(name => name.Trim()).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                WatchedMaps.Add(WatchedMapCard.Create(mapName));
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
                WindowsNotificationsEnabled = WindowsNotificationsCheck.IsChecked == true,
                NotifyOnJoin = NotifyJoinCheck.IsChecked == true,
                NotifyOnLeave = NotifyLeaveCheck.IsChecked == true,
                NotifyOnServerChange = NotifyServerCheck.IsChecked == true,
                NotifyOnMapChange = NotifyMapCheck.IsChecked == true,
                PlayNotificationSound = PlaySoundCheck.IsChecked == true,
                MapAlertsEnabled = MapAlertsCheck.IsChecked == true,
                MapAlertMinPlayers = _mapAlertMinPlayers,
                MapServerFilter = _mapServerFilter,
                ShowServerAddress = ShowAddressCheck.IsChecked == true,
                ShowExtraPlayerDetails = ShowExtraDetailsCheck.IsChecked == true,
                Language = _currentLanguage,
                Theme = _currentTheme,
                Nicknames = Players.Select(player => player.Nickname).ToList(),
                WatchedMaps = WatchedMaps.Select(map => map.Name).ToList()
            };

            _settingsService.Save(settings);
        }
        catch (Exception ex)
        {
            AddEvent($"Settings save error: {ex.Message}");
        }
    }

    private async System.Threading.Tasks.Task ScanAsync()
    {
        if (_scanInProgress || (Players.Count == 0 && WatchedMaps.Count == 0))
        {
            return;
        }

        _scanInProgress = true;
        SetScanningState();

        try
        {
            var snapshot = await _masterClient.ReadSnapshotAsync();
            var playerResults = DdnetMasterClient.FindTrackedPlayers(snapshot, Players.Select(player => player.Nickname));

            foreach (var player in Players)
            {
                var key = DdnetMasterClient.NormalizeName(player.Nickname);
                playerResults.TryGetValue(key, out var scanResult);
                ApplyScanResult(player, scanResult);
            }

            UpdateMapCards(snapshot);
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
                AddEvent(_currentLanguage == "ru" ? $"{player.Nickname} вышел" : $"{player.Nickname} left");
                if (NotifyLeaveCheck.IsChecked == true)
                {
                    ShowLocalNotification(
                        _currentLanguage == "ru" ? $"{player.Nickname} не в сети" : $"{player.Nickname} is offline",
                        _currentLanguage == "ru" ? "Ник больше не виден в списке серверов DDNet." : "The nickname is no longer visible in the DDNet server list.");
                }
            }

            player.SetOffline();
            return;
        }

        if (player.State != PlayerState.Online)
        {
            AddEvent(_currentLanguage == "ru" ? $"{player.Nickname} зашёл: {result.ServerName} / {result.MapName}" : $"{player.Nickname} joined {result.ServerName} / {result.MapName}");
            if (NotifyJoinCheck.IsChecked == true)
            {
                ShowLocalNotification(
                    _currentLanguage == "ru" ? $"{player.Nickname} в сети" : $"{player.Nickname} is online",
                    $"{result.ServerName}\n{result.MapName}");
            }
        }
        else if (!string.Equals(player.ServerAddress, result.ServerAddress, StringComparison.Ordinal))
        {
            AddEvent(_currentLanguage == "ru" ? $"{player.Nickname} сменил сервер: {result.ServerName} / {result.MapName}" : $"{player.Nickname} changed server: {result.ServerName} / {result.MapName}");
            if (NotifyServerCheck.IsChecked == true)
            {
                ShowLocalNotification(
                    _currentLanguage == "ru" ? $"{player.Nickname} сменил сервер" : $"{player.Nickname} changed server",
                    $"{result.ServerName}\n{result.MapName}");
            }
        }
        else if (!string.Equals(player.MapName, result.MapName, StringComparison.Ordinal))
        {
            AddEvent(_currentLanguage == "ru" ? $"{player.Nickname} сменил карту: {player.MapName} → {result.MapName}" : $"{player.Nickname} changed map: {player.MapName} → {result.MapName}");
            if (NotifyMapCheck.IsChecked == true)
            {
                ShowLocalNotification(
                    _currentLanguage == "ru" ? $"{player.Nickname} сменил карту" : $"{player.Nickname} changed map",
                    $"{player.MapName} → {result.MapName}");
            }
        }

        player.SetOnline(result);
    }

    private void UpdateMapCards(DdnetDataSnapshot snapshot)
    {
        foreach (var map in WatchedMaps)
        {
            var normalizedMap = DdnetMasterClient.NormalizeMapName(map.Name);
            var servers = snapshot.Servers
                .Where(server => string.Equals(DdnetMasterClient.NormalizeMapName(server.MapName), normalizedMap, StringComparison.OrdinalIgnoreCase))
                .Where(server => ServerMatchesFilter(server.ServerName))
                .Select(server => new MapServerInfo(server.ServerName, server.ServerAddress, server.MapName, server.GameType, server.PlayerCount, server.MaxPlayers))
                .ToList();

            map.UpdateServers(servers);
            ApplyMapAlert(map);
        }

        RefreshSelectedMapDetails();
    }

    private void ApplyMapAlert(WatchedMapCard map)
    {
        var active = map.ServersFound > 0 && map.PlayersTotal >= _mapAlertMinPlayers;

        if (active && !map.AlertActive && MapAlertsCheck.IsChecked == true)
        {
            var best = map.Servers.OrderByDescending(server => server.PlayerCount).FirstOrDefault();
            var message = best is null
                ? $"{map.PlayersTotal} players total"
                : $"{map.PlayersTotal} players total • {best.ServerName}";

            ShowLocalNotification(
                _currentLanguage == "ru" ? $"На карте {map.Name} кипиш" : $"{map.Name} is active",
                message);

            AddEvent(_currentLanguage == "ru" ? $"Кипиш на карте {map.Name}: {message}" : $"Map alert: {map.Name}: {message}");
        }

        map.AlertActive = active;
    }

    private bool ServerMatchesFilter(string serverName)
    {
        return _mapServerFilter switch
        {
            "GER" => serverName.Contains("GER", StringComparison.OrdinalIgnoreCase) || serverName.Contains("German", StringComparison.OrdinalIgnoreCase),
            "RUS" => serverName.Contains("RUS", StringComparison.OrdinalIgnoreCase) || serverName.Contains("Russian", StringComparison.OrdinalIgnoreCase),
            "GER_RUS" => serverName.Contains("GER", StringComparison.OrdinalIgnoreCase) || serverName.Contains("German", StringComparison.OrdinalIgnoreCase) || serverName.Contains("RUS", StringComparison.OrdinalIgnoreCase) || serverName.Contains("Russian", StringComparison.OrdinalIgnoreCase),
            _ => true
        };
    }

    private void RefreshSelectedMapDetails()
    {
        if (_selectedMap is null || SelectedMapTitleText is null)
        {
            return;
        }

        SelectedMapServers.Clear();

        foreach (var server in _selectedMap.Servers)
        {
            SelectedMapServers.Add(server);
        }

        SelectedMapTitleText.Text = _selectedMap.Name;
        SelectedMapSummaryText.Text = $"{_selectedMap.ServersFoundLine} • {_selectedMap.PlayersTotalLine}";
        NoMapServersState.Visibility = _selectedMap.ServersFound == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void SetScanningState()
    {
        SidebarStateText.Text = _currentLanguage == "ru" ? "Проверка..." : "Scanning...";
        SidebarStateText.Foreground = BrushFromHex("#FAD66B");
        TopStatusText.Text = _currentLanguage == "ru" ? "● Проверка" : "● Scanning";
        TopStatusText.Foreground = BrushFromHex("#FAD66B");
    }

    private void SetScanSuccessState()
    {
        var now = DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
        FooterLastScanText.Text = _currentLanguage == "ru" ? $"Последняя проверка: {now}" : $"Last scan: {now}";
        SidebarScanText.Text = _currentLanguage == "ru" ? $"Последняя проверка: {now}" : $"Last scan: {now}";
        FooterMasterText.Text = "master status: OK";
        SidebarStateText.Text = _currentLanguage == "ru" ? "Мониторинг активен" : "Monitoring active";
        SidebarStateText.Foreground = BrushFromHex("#3DFF9F");
        TopStatusText.Text = _currentLanguage == "ru" ? "● Мониторинг ON" : "● Monitoring ON";
        TopStatusText.Foreground = GetResourceBrush("StateOnlineBadgeForeground", "#C6FFE2");
    }

    private void SetScanErrorState(string message)
    {
        FooterMasterText.Text = "master status: error";
        SidebarStateText.Text = _currentLanguage == "ru" ? "Ошибка соединения" : "Connection error";
        SidebarStateText.Foreground = BrushFromHex("#FF4F6D");
        TopStatusText.Text = _currentLanguage == "ru" ? "● Ошибка соединения" : "● Connection error";
        TopStatusText.Foreground = BrushFromHex("#FF8FA2");
        AddEvent(_currentLanguage == "ru" ? $"Ошибка проверки: {message}" : $"Scan error: {message}");
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
        if (WindowsNotificationsCheck.IsChecked != true)
        {
            return;
        }

        try
        {
            _notificationService.Show(title, message);

            if (PlaySoundCheck.IsChecked == true)
            {
                System.Media.SystemSounds.Asterisk.Play();
            }
        }
        catch
        {
            try
            {
                if (PlaySoundCheck.IsChecked == true)
                {
                    System.Media.SystemSounds.Asterisk.Play();
                }
            }
            catch
            {
            }
        }

        AddEvent((_currentLanguage == "ru" ? "Уведомление" : "Notification") + $": {title}");
    }

    private void ShowDuplicateMessage(string message)
    {
        MessageBox.Show(this, message, AppMetadata.DisplayName, MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void UpdateStaticUi()
    {
        if (EmptyState is not null)
        {
            EmptyState.Visibility = Players.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        if (EmptyMapsState is not null)
        {
            EmptyMapsState.Visibility = WatchedMaps.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        FooterPlayersText.Text = _currentLanguage == "ru"
            ? Players.Count == 1 ? "1 ник" : $"{Players.Count} ников"
            : Players.Count == 1 ? "1 nickname" : $"{Players.Count} nicknames";

        FooterMapsText.Text = _currentLanguage == "ru"
            ? WatchedMaps.Count == 1 ? "1 карта" : $"{WatchedMaps.Count} карт"
            : WatchedMaps.Count == 1 ? "1 map" : $"{WatchedMaps.Count} maps";

        FooterIntervalText.Text = _currentLanguage == "ru" ? $"Проверка: {FormatSeconds(_cooldownSeconds)}" : $"Check every: {FormatSeconds(_cooldownSeconds)}";

        foreach (var player in Players)
        {
            player.RefreshLocalizedProperties();
        }

        foreach (var map in WatchedMaps)
        {
            map.RefreshCalculatedProperties();
        }

        RefreshSelectedMapDetails();
    }

    private void ApplyCardDisplaySettings()
    {
        foreach (var player in Players)
        {
            player.UpdateDisplaySettings(_showServerAddress, _showExtraDetails);
        }
    }

    private void ApplyLocalization()
    {
        LocalizationService.CurrentLanguage = _currentLanguage;
        var ru = _currentLanguage == "ru";

        Title = $"{AppMetadata.DisplayName} {AppMetadata.Version}";
        WindowTitleText.Text = Title;

        AboutNavText.Text = ru ? "О приложении" : "About";
        HomeNavText.Text = ru ? "Главное меню" : "Main menu";
        NotificationsNavText.Text = ru ? "Уведомления" : "Notifications";
        OptionsNavText.Text = ru ? "Настройки" : "Options";
        SidebarEventsTitle.Text = ru ? "События" : "Recent events";

        HomeHeaderText.Text = ru ? "Главное меню" : "Main menu";
        HomeSubheaderText.Text = ru ? "Отслеживайте ники и карты DDNet из публичного списка серверов." : "Watch DDNet players and maps from the public server list.";
        AddNicknameButton.Content = ru ? "+ Добавить ник" : "+ Add nickname";
        AddMapButton.Content = ru ? "+ Добавить карту" : "+ Add map";
        CardsTitleText.Text = ru ? "Отслеживаемые ники" : "Watched nicknames";
        MapsTitleText.Text = ru ? "Отслеживаемые карты" : "Watched maps";
        MapsSubtitleText.Text = ru ? "Откройте папку карты, чтобы увидеть активные серверы." : "Open a map folder to see active servers.";
        ScanNowButton.Content = ru ? "Проверить сейчас" : "Scan now";
        ScanMapsButton.Content = ru ? "Проверить сейчас" : "Scan now";
        EmptyStateTitle.Text = ru ? "Пока нет ников" : "No nicknames yet";
        EmptyStateText.Text = ru ? "Добавьте ник, чтобы начать мониторинг." : "Add a nickname to start monitoring.";
        EmptyMapsTitle.Text = ru ? "Пока нет карт" : "No maps yet";
        EmptyMapsText.Text = ru ? "Добавьте карту, чтобы отслеживать активные серверы." : "Add a map to watch active servers.";
        BackToMapsButton.Content = ru ? "← Назад" : "← Back";
        NoMapServersTitle.Text = ru ? "Нет активных серверов" : "No active servers";
        NoMapServersText.Text = ru ? "Этой карты сейчас нет с выбранным фильтром серверов." : "This map is not active on the selected server filter.";

        NotificationsHeaderText.Text = ru ? "Уведомления" : "Notifications";
        NotificationsSubheaderText.Text = ru ? "Выберите, какие локальные уведомления Windows нужно показывать." : "Choose which local Windows notifications should be shown.";
        WindowsNotificationsTitle.Text = ru ? "Уведомления Windows" : "Windows notifications";
        WindowsNotificationsCheck.Content = ru ? "Вкл" : "On";
        NotifyJoinCheck.Content = ru ? "Уведомлять, когда игрок заходит" : "Notify when a player joins";
        NotifyLeaveCheck.Content = ru ? "Уведомлять, когда игрок выходит" : "Notify when a player leaves";
        NotifyServerCheck.Content = ru ? "Уведомлять, когда игрок меняет сервер" : "Notify when a player changes server";
        NotifyMapCheck.Content = ru ? "Уведомлять, когда игрок меняет карту" : "Notify when a player changes map";
        MapAlertsCheck.Content = ru ? "Уведомлять, когда на карте набрался онлайн" : "Notify when watched maps reach the selected online";
        PlaySoundCheck.Content = ru ? "Воспроизводить звук вместе с уведомлением" : "Play a sound with notifications";
        DiscordTitleText.Text = "Discord";
        DiscordInfoText.Text = ru ? "В разработке" : "In development";
        TelegramTitleText.Text = "Telegram";
        TelegramInfoText.Text = ru ? "В разработке" : "In development";

        OptionsHeaderText.Text = ru ? "Настройки" : "Options";
        AppearanceTitleText.Text = ru ? "Внешний вид" : "Appearance";
        LanguageLabelText.Text = ru ? "Язык" : "Language";
        ThemeLabelText.Text = ru ? "Тема" : "Theme";
        ThemeLightButton.Content = ru ? "Светлая" : "Light";
        ThemeDarkButton.Content = ru ? "Тёмная" : "Dark";
        MonitoringTitleText.Text = ru ? "Мониторинг" : "Monitoring";
        IntervalLabelText.Text = ru ? "Интервал проверки" : "Scan interval";
        MapsOptionsTitleText.Text = ru ? "Карты" : "Maps";
        MapAlertPlayersLabelText.Text = ru ? "Онлайн для сигнала" : "Alert online";
        MapServerFilterLabelText.Text = ru ? "Фильтр серверов" : "Server filter";
        CardsOptionsTitleText.Text = ru ? "Карточки" : "Card display";
        ShowAddressCheck.Content = ru ? "Показывать адрес сервера в деталях" : "Show server address in details";
        ShowExtraDetailsCheck.Content = ru ? "Показывать дополнительные данные игрока" : "Show extra player details";

        AboutHeaderText.Text = ru ? "О приложении" : "About";
        AboutSummaryText.Text = ru ? "DDNetNW проверяет выбранные ники и карты по публичному списку серверов DDNet." : "DDNetNW checks selected nicknames and maps against the public DDNet server list.";
        AboutVersionText.Text = (ru ? "Версия: " : "Version: ") + AppMetadata.Version;
        AboutCreatorText.Text = AppMetadata.CreatorLine;
        AboutHowItWorksTitleText.Text = ru ? "Как это работает" : "How it works";
        AboutHowItWorksText.Text = ru
            ? "Приложение читает DDNet servers.json, проверяет игроков и карты, а затем локально обновляет карточки."
            : "The app reads DDNet servers.json, scans players and maps, and updates local cards.";
        AboutFeaturesTitleText.Text = ru ? "Основные функции" : "Main features";
        AboutFeaturesText.Text = ru
            ? "Отслеживание игроков, папки карт, уведомления по онлайну карты, фильтр серверов, локальные уведомления, темы и переключение языка."
            : "Player tracking, map folders, map online alerts, server filtering, local notifications, custom themes and bilingual UI.";
        AboutDataTitleText.Text = ru ? "Источник данных" : "Data source";
        AboutDataText.Text = ru
            ? "DDNetNW использует публичный список master-серверов DDNet. Приложению не нужен вход в аккаунт и не нужен отдельный сервер."
            : "DDNetNW uses the public DDNet master server list. It does not log into DDNet and does not need a private server.";
        AboutStorageTitleText.Text = ru ? "Хранение и приватность" : "Storage and privacy";
        AboutStorageText.Text = ru
            ? "Настройки хранятся локально в папке AppData пользователя. Приложение не отправляет отслеживаемые ники или карты сторонним сервисам."
            : "Settings are stored locally in the user AppData folder. The app does not send watched nicknames or maps to any third-party service.";
        AboutLimitationsTitleText.Text = ru ? "Ограничения" : "Limitations";
        AboutLimitationsText.Text = ru
            ? "Ники DDNet не являются аккаунтами. Приложение отслеживает публичный текст ника и активность карт/серверов, поэтому визуально похожие имена всё равно могут быть разными игроками."
            : "DDNet nicknames are not accounts. The app tracks public nickname text and public map/server activity, so visually similar names can still be different players.";

        UpdateStaticUi();
        CooldownValueText.Text = ru ? $"{_cooldownSeconds} с" : $"{_cooldownSeconds} s";
        MapAlertPlayersValueText.Text = _mapAlertMinPlayers.ToString(CultureInfo.InvariantCulture);
    }

    private void ApplyTheme(string theme)
    {
        var palette = theme == "light"
            ? new Dictionary<string, string>
            {
                ["AppBackground"] = "#EEF2F7",
                ["PanelBackground"] = "#F8FBFF",
                ["CardBackground"] = "#FFFFFF",
                ["TitleBarBackground"] = "#E8EEF7",
                ["TextMain"] = "#112033",
                ["TextMuted"] = "#5C718C",
                ["BlueAccent"] = "#2970E3",
                ["BlueBorder"] = "#B8D0EF",
                ["GreenAccent"] = "#139B67",
                ["RedAccent"] = "#D94B63",
                ["YellowAccent"] = "#D89A22",
                ["PrimaryButtonBackground"] = "#2F6FD6",
                ["PrimaryButtonHoverBackground"] = "#245FC0",
                ["PrimaryButtonPressedBackground"] = "#1E4FA0",
                ["PrimaryButtonBorder"] = "#2C66C3",
                ["PrimaryButtonText"] = "#FFFFFF",
                ["SecondaryButtonBackground"] = "#EAF3FF",
                ["SecondaryButtonHoverBackground"] = "#DDEBFF",
                ["SecondaryButtonPressedBackground"] = "#D1E3FC",
                ["SecondaryButtonBorder"] = "#9DBEE7",
                ["SecondaryButtonText"] = "#143055",
                ["NavBackground"] = "#F6FAFF",
                ["NavHoverBackground"] = "#ECF4FF",
                ["NavSelectedBackground"] = "#DBEBFF",
                ["NavBorder"] = "#D7E5F6",
                ["NavSelectedBorder"] = "#8EB7F0",
                ["NavText"] = "#1C365A",
                ["NavActiveText"] = "#143055",
                ["InputBackground"] = "#FFFFFF",
                ["InputBorder"] = "#B8D0EF",
                ["InputText"] = "#112033",
                ["ComboBackground"] = "#FFFFFF",
                ["ComboBorder"] = "#AFC7E7",
                ["ComboForeground"] = "#112033",
                ["SliderTrackEmpty"] = "#D7E5F6",
                ["SliderTrackFilled"] = "#2970E3",
                ["SliderThumbFill"] = "#FFFFFF",
                ["ScrollThumbBackground"] = "#AFC7E7",
                ["ScrollThumbBorder"] = "#7CA3D9",
                ["StateOnlineBorder"] = "#7DCFA3",
                ["StateOnlineBadgeBackground"] = "#E8F7EF",
                ["StateOnlineBadgeForeground"] = "#156E49",
                ["StateOfflineBorder"] = "#EAA0AF",
                ["StateOfflineBadgeBackground"] = "#FFF1F4",
                ["StateOfflineBadgeForeground"] = "#9B3550",
                ["StateWaitingBorder"] = "#D7B56F",
                ["StateWaitingBadgeBackground"] = "#FFF7E3",
                ["StateWaitingBadgeForeground"] = "#8D6A1A"
            }
            : new Dictionary<string, string>
            {
                ["AppBackground"] = "#07111F",
                ["PanelBackground"] = "#0B1628",
                ["CardBackground"] = "#101A2B",
                ["TitleBarBackground"] = "#091425",
                ["TextMain"] = "#F3F7FF",
                ["TextMuted"] = "#8FA3BF",
                ["BlueAccent"] = "#3A86FF",
                ["BlueBorder"] = "#244A78",
                ["GreenAccent"] = "#3DFF9F",
                ["RedAccent"] = "#FF4F6D",
                ["YellowAccent"] = "#FAD66B",
                ["PrimaryButtonBackground"] = "#1E4E86",
                ["PrimaryButtonHoverBackground"] = "#2862A6",
                ["PrimaryButtonPressedBackground"] = "#194777",
                ["PrimaryButtonBorder"] = "#417AB8",
                ["PrimaryButtonText"] = "#FFFFFF",
                ["SecondaryButtonBackground"] = "#0F223A",
                ["SecondaryButtonHoverBackground"] = "#17314F",
                ["SecondaryButtonPressedBackground"] = "#102946",
                ["SecondaryButtonBorder"] = "#2B4E78",
                ["SecondaryButtonText"] = "#F3F7FF",
                ["NavBackground"] = "#0E1D34",
                ["NavHoverBackground"] = "#132843",
                ["NavSelectedBackground"] = "#17365A",
                ["NavBorder"] = "#21456E",
                ["NavSelectedBorder"] = "#4A94FF",
                ["NavText"] = "#EAF2FF",
                ["NavActiveText"] = "#FFFFFF",
                ["InputBackground"] = "#091424",
                ["InputBorder"] = "#2A4E7B",
                ["InputText"] = "#F3F7FF",
                ["ComboBackground"] = "#091424",
                ["ComboBorder"] = "#2A4E7B",
                ["ComboForeground"] = "#F3F7FF",
                ["SliderTrackEmpty"] = "#10223B",
                ["SliderTrackFilled"] = "#3A86FF",
                ["SliderThumbFill"] = "#F3F7FF",
                ["ScrollThumbBackground"] = "#2A4E7C",
                ["ScrollThumbBorder"] = "#3E74B2",
                ["StateOnlineBorder"] = "#52D89A",
                ["StateOnlineBadgeBackground"] = "#103225",
                ["StateOnlineBadgeForeground"] = "#C6FFE2",
                ["StateOfflineBorder"] = "#F27A92",
                ["StateOfflineBadgeBackground"] = "#34111B",
                ["StateOfflineBadgeForeground"] = "#FFD2DA",
                ["StateWaitingBorder"] = "#D4A94B",
                ["StateWaitingBadgeBackground"] = "#342A12",
                ["StateWaitingBadgeForeground"] = "#FFE9B4"
            };

        foreach (var pair in palette)
        {
            Application.Current.Resources[pair.Key] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(pair.Value));
        }

        UpdateStaticUi();
    }

    private static void SelectRadioByTag(IEnumerable<RadioButton> buttons, string tag)
    {
        foreach (var button in buttons)
        {
            if (string.Equals(button.Tag?.ToString(), tag, StringComparison.OrdinalIgnoreCase))
            {
                button.IsChecked = true;
                return;
            }
        }
    }

    private string FormatSeconds(int seconds)
    {
        if (_currentLanguage == "ru")
        {
            return $"{seconds} с";
        }

        return seconds == 1 ? "1 second" : $"{seconds} seconds";
    }

    private static SolidColorBrush BrushFromHex(string hex)
    {
        return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
    }

    private static Brush GetResourceBrush(string key, string fallbackHex)
    {
        if (Application.Current.Resources[key] is Brush brush)
        {
            return brush;
        }

        return BrushFromHex(fallbackHex);
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            ToggleMaximize();
            return;
        }

        if (e.LeftButton == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void MaximizeButton_Click(object sender, RoutedEventArgs e)
    {
        ToggleMaximize();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void ToggleMaximize()
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    protected override void OnClosed(EventArgs e)
    {
        SaveSettings();
        _scanTimer.Stop();
        _notificationService.Dispose();
        _masterClient.Dispose();
        base.OnClosed(e);
    }
}
