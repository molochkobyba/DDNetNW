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
    private readonly DispatcherTimer _searchDebounceTimer = new();
    private readonly NicknameSearchService _nicknameSearchService;

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
    private string _lastSearchNickname = string.Empty;
    private PlayerScanResult? _lastSearchResult;
    private bool _lastSearchOnline;
    private int _searchRequestVersion;

    public ObservableCollection<PlayerCard> Players { get; } = new();
    public ObservableCollection<WatchedMapCard> WatchedMaps { get; } = new();
    public ObservableCollection<MapServerInfo> SelectedMapServers { get; } = new();
    public ObservableCollection<string> Events { get; } = new();

    public MainWindow()
    {
        _nicknameSearchService = new NicknameSearchService(_masterClient);
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
        ConfigureQuickSearchDebounce();
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
            AddEvent(Text("Application started.", "Приложение запущено.", "Aplicación iniciada."));

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

    private void ConfigureQuickSearchDebounce()
    {
        _searchDebounceTimer.Interval = TimeSpan.FromSeconds(1);
        _searchDebounceTimer.Tick += async (_, _) =>
        {
            _searchDebounceTimer.Stop();
            await SearchNicknameAsync();
        };
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
        // v1.35: nicknames are added through Quick Search, not through the old Add Nickname button.
        AddNicknameButton.Visibility = Visibility.Collapsed;
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
            ShowDuplicateMessage(Text("This nickname is already in the list.", "Такой ник уже есть в списке.", "Este nick ya está en la lista."));
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
            ShowDuplicateMessage(Text("This nickname is already in the list.", "Такой ник уже есть в списке.", "Este nick ya está en la lista."));
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

    private async void SearchNickname_Click(object sender, RoutedEventArgs e)
    {
        _searchDebounceTimer.Stop();
        await SearchNicknameAsync();
    }

    private async void SearchNicknameBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        e.Handled = true;
        _searchDebounceTimer.Stop();
        await SearchNicknameAsync();
    }

    private void SearchNicknameBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_windowLoaded)
        {
            return;
        }

        _searchRequestVersion++;
        _searchDebounceTimer.Stop();

        if (string.IsNullOrWhiteSpace(SearchNicknameBox.Text))
        {
            HideSearchResult();
            return;
        }

        _searchDebounceTimer.Start();
    }

    private async System.Threading.Tasks.Task SearchNicknameAsync()
    {
        var nickname = SearchNicknameBox.Text.Trim();
        var requestVersion = ++_searchRequestVersion;

        if (string.IsNullOrWhiteSpace(nickname))
        {
            HideSearchResult();
            return;
        }

        SetSearchLoading(nickname);

        try
        {
            SearchNicknameButton.IsEnabled = false;
            var result = await _nicknameSearchService.SearchExactAsync(nickname);

            if (requestVersion != _searchRequestVersion)
            {
                return;
            }

            ShowSearchResult(result);
        }
        catch (Exception ex)
        {
            if (requestVersion == _searchRequestVersion)
            {
                ShowSearchError(nickname, ex.Message);
            }
        }
        finally
        {
            SearchNicknameButton.IsEnabled = true;
        }
    }

    private void AddSearchResult_Click(object sender, RoutedEventArgs e)
    {
        var nickname = string.IsNullOrWhiteSpace(_lastSearchNickname)
            ? SearchNicknameBox.Text.Trim()
            : _lastSearchNickname.Trim();

        if (string.IsNullOrWhiteSpace(nickname))
        {
            return;
        }

        var normalized = DdnetMasterClient.NormalizeName(nickname);

        if (Players.Any(player => DdnetMasterClient.NormalizeName(player.Nickname) == normalized))
        {
            ShowDuplicateMessage(Text("This nickname is already in the list.", "Такой ник уже есть в списке.", "Este nick ya está en la lista."));
            RefreshSearchResultVisual();
            return;
        }

        var card = PlayerCard.CreateUnknown(nickname);
        card.UpdateDisplaySettings(_showServerAddress, _showExtraDetails);

        if (_lastSearchResult is not null && _lastSearchOnline)
        {
            card.SetOnline(_lastSearchResult);
        }

        Players.Add(card);
        AddEvent(_currentLanguage == "ru" ? $"Добавлен ник из поиска: {nickname}" : $"Added nickname from search: {nickname}");
        SaveSettings();
        RefreshSearchResultVisual();
    }

    private void ClearSearchResult_Click(object sender, RoutedEventArgs e)
    {
        HideSearchResult();
    }

    private void SetSearchLoading(string nickname)
    {
        _lastSearchNickname = nickname;
        _lastSearchResult = null;
        _lastSearchOnline = false;

        SearchResultStrip.Visibility = Visibility.Visible;
        SearchResultStrip.Background = GetResourceBrush("StateWaitingBadgeBackground", "#342A12");
        SearchResultStrip.BorderBrush = GetResourceBrush("StateWaitingBorder", "#D4A94B");
        SearchResultStatusDot.Text = "●";
        SearchResultStatusDot.Foreground = GetResourceBrush("StateWaitingBorder", "#D4A94B");
        SearchResultNicknameText.Text = nickname;
        SearchResultClanText.Text = Text("Searching...", "Поиск...", "Buscando...");
        SearchResultClanText.Visibility = Visibility.Visible;
        SearchResultMapText.Visibility = Visibility.Collapsed;
        SearchResultAddButton.IsEnabled = false;
        SearchResultAddButton.Content = Text("Checking", "Проверка", "Comprobando");
    }

    private void ShowSearchResult(QuickNicknameSearchResult searchResult)
    {
        _lastSearchResult = searchResult.Player;
        _lastSearchOnline = searchResult.IsOnline;
        _lastSearchNickname = searchResult.DisplayNickname;

        SearchResultStrip.Visibility = Visibility.Visible;
        RefreshSearchResultVisual();

        AddEvent(_lastSearchOnline
            ? Text($"Search: {_lastSearchNickname} is online", $"Поиск: {_lastSearchNickname} найден онлайн", $"Búsqueda: {_lastSearchNickname} está en línea")
            : Text($"Search: {searchResult.RequestedNickname} is not online", $"Поиск: {searchResult.RequestedNickname} не найден онлайн", $"Búsqueda: {searchResult.RequestedNickname} no está en línea"));
    }

    private void ShowSearchError(string nickname, string message)
    {
        _lastSearchNickname = nickname;
        _lastSearchResult = null;
        _lastSearchOnline = false;

        SearchResultStrip.Visibility = Visibility.Visible;
        SearchResultStrip.Background = GetResourceBrush("StateOfflineBadgeBackground", "#34111B");
        SearchResultStrip.BorderBrush = GetResourceBrush("StateOfflineBorder", "#F27A92");
        SearchResultStatusDot.Text = "●";
        SearchResultStatusDot.Foreground = GetResourceBrush("StateOfflineBorder", "#F27A92");
        SearchResultNicknameText.Text = nickname;
        SearchResultClanText.Text = Text("Search error", "Ошибка поиска", "Error de búsqueda");
        SearchResultClanText.Visibility = Visibility.Visible;
        SearchResultMapText.Text = message;
        SearchResultMapText.Visibility = Visibility.Visible;
        SearchResultAddButton.IsEnabled = false;
        SearchResultAddButton.Content = Text("Error", "Ошибка", "Error");
        AddEvent(_currentLanguage == "ru" ? $"Ошибка поиска: {message}" : $"Search error: {message}");
    }

    private void HideSearchResult()
    {
        _lastSearchNickname = string.Empty;
        _lastSearchResult = null;
        _lastSearchOnline = false;
        SearchResultStrip.Visibility = Visibility.Collapsed;
    }

    private void RefreshSearchResultVisual()
    {
        if (SearchResultStrip is null || SearchResultStrip.Visibility != Visibility.Visible)
        {
            return;
        }

        var duplicate = !string.IsNullOrWhiteSpace(_lastSearchNickname) && Players.Any(player =>
            DdnetMasterClient.NormalizeName(player.Nickname) == DdnetMasterClient.NormalizeName(_lastSearchNickname));

        if (_lastSearchOnline && _lastSearchResult is not null)
        {
            var clan = string.IsNullOrWhiteSpace(_lastSearchResult.Clan) ? "—" : _lastSearchResult.Clan;
            SearchResultStrip.Background = GetResourceBrush("StateOnlineBadgeBackground", "#103225");
            SearchResultStrip.BorderBrush = GetResourceBrush("StateOnlineBorder", "#52D89A");
            SearchResultStatusDot.Text = "●";
            SearchResultStatusDot.Foreground = GetResourceBrush("StateOnlineBorder", "#52D89A");
            SearchResultNicknameText.Text = _lastSearchResult.Nickname;
            SearchResultClanText.Text = $"Clan: {clan}";
            SearchResultClanText.Visibility = Visibility.Visible;
            SearchResultMapText.Text = $"Map: {_lastSearchResult.MapName}";
            SearchResultMapText.Visibility = Visibility.Visible;
        }
        else
        {
            SearchResultStrip.Background = GetResourceBrush("CardBackground", "#101A2B");
            SearchResultStrip.BorderBrush = GetResourceBrush("StateOfflineBorder", "#F27A92");
            SearchResultStatusDot.Text = "○";
            SearchResultStatusDot.Foreground = GetResourceBrush("StateOfflineBorder", "#F27A92");
            SearchResultNicknameText.Text = _lastSearchNickname;
            SearchResultClanText.Text = Text("Not online", "Не найден онлайн", "No está en línea");
            SearchResultClanText.Visibility = Visibility.Visible;
            SearchResultMapText.Visibility = Visibility.Collapsed;
        }

        SearchResultAddButton.Content = duplicate
            ? (Text("Added", "Добавлен", "Añadido"))
            : (Text("+ Watch", "+ Добавить", "+ Vigilar"));
        SearchResultAddButton.IsEnabled = !duplicate && !string.IsNullOrWhiteSpace(_lastSearchNickname);
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
        CooldownValueText.Text = IsRu ? $"{_cooldownSeconds} с" : $"{_cooldownSeconds} s";
        FooterIntervalText.Text = Text($"Check every: {FormatSeconds(_cooldownSeconds)}", $"Проверка: {FormatSeconds(_cooldownSeconds)}", $"Cada: {FormatSeconds(_cooldownSeconds)}");
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

    private void ChangeMapServerFilter_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SelectRegionsWindow
        {
            Owner = this,
            LanguageCode = _currentLanguage,
            SelectedFilter = _mapServerFilter
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        _mapServerFilter = dialog.SelectedFilter;
        UpdateMapServerFilterText();
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
            CooldownValueText.Text = IsRu ? $"{_cooldownSeconds} с" : $"{_cooldownSeconds} s";
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

            SelectRadioByTag(new[] { LanguageEnglishButton, LanguageRussianButton, LanguageSpanishButton }, _currentLanguage);
            SelectRadioByTag(new[] { ThemeLightButton, ThemeDarkButton }, _currentTheme);
            UpdateMapServerFilterText();

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
                .Where(ServerMatchesFilter)
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

    private bool ServerMatchesFilter(ServerSnapshot server)
    {
        var selected = ParseSelectedRegions(_mapServerFilter).ToList();

        if (selected.Count == 0)
        {
            return true;
        }

        return selected.Any(region => ServerMatchesRegion(server, region));
    }

    private static bool ServerMatchesRegion(ServerSnapshot server, string region)
    {
        if (string.Equals(server.Location, region, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return server.ServerName.Contains(region, StringComparison.OrdinalIgnoreCase);
    }

    private static IEnumerable<string> ParseSelectedRegions(string filter)
    {
        if (string.IsNullOrWhiteSpace(filter) || string.Equals(filter, "Any", StringComparison.OrdinalIgnoreCase))
        {
            return Array.Empty<string>();
        }

        if (string.Equals(filter, "GER_RUS", StringComparison.OrdinalIgnoreCase))
        {
            return new[] { "GER", "RUS" };
        }

        return filter
            .Split(new[] { ',', '|', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(region => region.Trim().ToUpperInvariant())
            .Where(region => region.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase);
    }

    private string FormatMapServerFilter()
    {
        var selected = ParseSelectedRegions(_mapServerFilter).ToList();
        return selected.Count == 0 ? Text("Any", "Любой", "Cualquiera") : string.Join(" | ", selected);
    }

    private void UpdateMapServerFilterText()
    {
        if (MapServerFilterValueText is not null)
        {
            MapServerFilterValueText.Text = FormatMapServerFilter();
        }
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
        SidebarStateText.Text = Text("Scanning...", "Проверка...", "Escaneando...");
        SidebarStateText.Foreground = BrushFromHex("#FAD66B");
        TopStatusText.Text = Text("● Scanning", "● Проверка", "● Escaneando");
        TopStatusText.Foreground = BrushFromHex("#FAD66B");
    }

    private void SetScanSuccessState()
    {
        var now = DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
        FooterLastScanText.Text = Text($"Last scan: {now}", $"Последняя проверка: {now}", $"Último escaneo: {now}");
        SidebarScanText.Text = Text($"Last scan: {now}", $"Последняя проверка: {now}", $"Último escaneo: {now}");
        FooterMasterText.Text = "master status: OK";
        SidebarStateText.Text = Text("Monitoring active", "Мониторинг активен", "Monitoreo activo");
        SidebarStateText.Foreground = BrushFromHex("#3DFF9F");
        TopStatusText.Text = Text("● Monitoring ON", "● Мониторинг ON", "● Monitoreo ON");
        TopStatusText.Foreground = GetResourceBrush("StateOnlineBadgeForeground", "#C6FFE2");
    }

    private void SetScanErrorState(string message)
    {
        FooterMasterText.Text = "master status: error";
        SidebarStateText.Text = Text("Connection error", "Ошибка соединения", "Error de conexión");
        SidebarStateText.Foreground = BrushFromHex("#FF4F6D");
        TopStatusText.Text = Text("● Connection error", "● Ошибка соединения", "● Error de conexión");
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

        AddEvent((Text("Notification", "Уведомление", "Notificación")) + $": {title}");
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

        FooterPlayersText.Text = IsRu
            ? Players.Count == 1 ? "1 ник" : $"{Players.Count} ников"
            : IsEs
                ? Players.Count == 1 ? "1 nick" : $"{Players.Count} nicks"
                : Players.Count == 1 ? "1 nickname" : $"{Players.Count} nicknames";

        FooterMapsText.Text = IsRu
            ? WatchedMaps.Count == 1 ? "1 карта" : $"{WatchedMaps.Count} карт"
            : IsEs
                ? WatchedMaps.Count == 1 ? "1 mapa" : $"{WatchedMaps.Count} mapas"
                : WatchedMaps.Count == 1 ? "1 map" : $"{WatchedMaps.Count} maps";

        FooterIntervalText.Text = Text($"Check every: {FormatSeconds(_cooldownSeconds)}", $"Проверка: {FormatSeconds(_cooldownSeconds)}", $"Cada: {FormatSeconds(_cooldownSeconds)}");

        foreach (var player in Players)
        {
            player.RefreshLocalizedProperties();
        }

        foreach (var map in WatchedMaps)
        {
            map.RefreshCalculatedProperties();
        }

        RefreshSearchResultVisual();
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

        Title = $"{AppMetadata.DisplayName} {AppMetadata.Version}";
        WindowTitleText.Text = Title;

        AboutNavText.Text = Text("About", "О приложении", "Acerca de");
        HomeNavText.Text = Text("Main menu", "Главное меню", "Menú principal");
        NotificationsNavText.Text = Text("Notifications", "Уведомления", "Notificaciones");
        OptionsNavText.Text = Text("Options", "Настройки", "Opciones");
        SidebarEventsTitle.Text = Text("Recent events", "События", "Eventos recientes");

        HomeHeaderText.Text = Text("Main menu", "Главное меню", "Menú principal");
        HomeSubheaderText.Text = Text(
            "Watch DDNet players and maps from the public server list.",
            "Отслеживайте ники и карты DDNet из публичного списка серверов.",
            "Sigue jugadores y mapas de DDNet desde la lista pública de servidores.");
        AddNicknameButton.Content = Text("+ Add nickname", "+ Добавить ник", "+ Añadir nick");
        AddMapButton.Content = Text("+ Add map", "+ Добавить карту", "+ Añadir mapa");
        CardsTitleText.Text = Text("Watched nicknames", "Отслеживаемые ники", "Nicks vigilados");
        MapsTitleText.Text = Text("Watched maps", "Отслеживаемые карты", "Mapas vigilados");
        MapsSubtitleText.Text = Text(
            "Open a map folder to see active servers.",
            "Откройте папку карты, чтобы увидеть активные серверы.",
            "Abre un mapa para ver servidores activos.");
        ScanNowButton.Content = Text("Scan now", "Проверить сейчас", "Escanear ahora");
        ScanMapsButton.Content = Text("Scan now", "Проверить сейчас", "Escanear ahora");
        EmptyStateTitle.Text = Text("No nicknames yet", "Пока нет ников", "Aún no hay nicks");
        EmptyStateText.Text = Text(
            "Use quick search above to add a nickname.",
            "Используйте быстрый поиск выше, чтобы добавить ник.",
            "Usa la búsqueda rápida de arriba para añadir un nick.");
        EmptyMapsTitle.Text = Text("No maps yet", "Пока нет карт", "Aún no hay mapas");
        EmptyMapsText.Text = Text(
            "Add a map to watch active servers.",
            "Добавьте карту, чтобы отслеживать активные серверы.",
            "Añade un mapa para seguir servidores activos.");
        SearchTitleText.Text = Text("Quick nickname search", "Быстрый поиск ника", "Búsqueda rápida de nick");
        SearchNicknameButton.Content = Text("Search now", "Найти сейчас", "Buscar ahora");
        SearchResultClearButton.Content = "×";
        BackToMapsButton.Content = Text("← Back", "← Назад", "← Atrás");
        NoMapServersTitle.Text = Text("No active servers", "Нет активных серверов", "No hay servidores activos");
        NoMapServersText.Text = Text(
            "This map is not active on the selected server filter.",
            "Этой карты сейчас нет с выбранным фильтром серверов.",
            "Este mapa no está activo con el filtro de servidores seleccionado.");

        NotificationsHeaderText.Text = Text("Notifications", "Уведомления", "Notificaciones");
        NotificationsSubheaderText.Text = Text(
            "Choose which local Windows notifications should be shown.",
            "Выберите, какие локальные уведомления Windows нужно показывать.",
            "Elige qué notificaciones locales de Windows mostrar.");
        WindowsNotificationsTitle.Text = Text("Windows notifications", "Уведомления Windows", "Notificaciones de Windows");
        WindowsNotificationsCheck.Content = Text("On", "Вкл", "Activado");
        NotifyJoinCheck.Content = Text("Notify when a player joins", "Уведомлять, когда игрок заходит", "Avisar cuando un jugador entra");
        NotifyLeaveCheck.Content = Text("Notify when a player leaves", "Уведомлять, когда игрок выходит", "Avisar cuando un jugador sale");
        NotifyServerCheck.Content = Text("Notify when a player changes server", "Уведомлять, когда игрок меняет сервер", "Avisar cuando cambia de servidor");
        NotifyMapCheck.Content = Text("Notify when a player changes map", "Уведомлять, когда игрок меняет карту", "Avisar cuando cambia de mapa");
        MapAlertsCheck.Content = Text("Notify when watched maps reach the selected online", "Уведомлять, когда на карте набрался онлайн", "Avisar cuando un mapa vigilado alcance el online elegido");
        PlaySoundCheck.Content = Text("Play a sound with notifications", "Воспроизводить звук вместе с уведомлением", "Reproducir sonido con las notificaciones");
        DiscordTitleText.Text = "Discord";
        DiscordInfoText.Text = Text("In development", "В разработке", "En desarrollo");
        TelegramTitleText.Text = "Telegram";
        TelegramInfoText.Text = Text("In development", "В разработке", "En desarrollo");

        OptionsHeaderText.Text = Text("Options", "Настройки", "Opciones");
        AppearanceTitleText.Text = Text("Appearance", "Внешний вид", "Apariencia");
        LanguageLabelText.Text = Text("Language", "Язык", "Idioma");
        ThemeLabelText.Text = Text("Theme", "Тема", "Tema");
        ThemeLightButton.Content = Text("Light", "Светлая", "Claro");
        ThemeDarkButton.Content = Text("Dark", "Тёмная", "Oscuro");
        MonitoringTitleText.Text = Text("Monitoring", "Мониторинг", "Monitoreo");
        IntervalLabelText.Text = Text("Scan interval", "Интервал проверки", "Intervalo de escaneo");
        MapsOptionsTitleText.Text = Text("Maps", "Карты", "Mapas");
        MapAlertPlayersLabelText.Text = Text("Alert online", "Онлайн для сигнала", "Online para aviso");
        MapServerFilterLabelText.Text = Text("Tracked regions", "Отслеживаемые серверы", "Regiones vigiladas");
        ChangeMapServerFilterButton.Content = Text("Change", "Изменить", "Cambiar");
        CardsOptionsTitleText.Text = Text("Card display", "Карточки", "Tarjetas");
        ShowAddressCheck.Content = Text("Show server address in details", "Показывать адрес сервера в деталях", "Mostrar dirección del servidor en detalles");
        ShowExtraDetailsCheck.Content = Text("Show extra player details", "Показывать дополнительные данные игрока", "Mostrar detalles extra del jugador");

        AboutHeaderText.Text = Text("About", "О приложении", "Acerca de");
        AboutSummaryText.Text = Text(
            "DDNetNW checks selected nicknames and maps against the public DDNet server list.",
            "DDNetNW проверяет выбранные ники и карты по публичному списку серверов DDNet.",
            "DDNetNW comprueba nicks y mapas elegidos usando la lista pública de servidores DDNet.");
        AboutVersionText.Text = Text("Version: ", "Версия: ", "Versión: ") + AppMetadata.Version;
        AboutCreatorText.Text = AppMetadata.CreatorLine;
        AboutHowItWorksTitleText.Text = Text("How it works", "Как это работает", "Cómo funciona");
        AboutHowItWorksText.Text = Text(
            "The app reads DDNet servers.json, scans players and maps, and updates local cards.",
            "Приложение читает DDNet servers.json, проверяет игроков и карты, а затем локально обновляет карточки.",
            "La app lee DDNet servers.json, escanea jugadores y mapas, y actualiza las tarjetas locales.");
        AboutFeaturesTitleText.Text = Text("Main features", "Основные функции", "Funciones principales");
        AboutFeaturesText.Text = Text(
            "Player tracking, map folders, map online alerts, server filtering, local notifications, custom themes and multilingual UI.",
            "Отслеживание игроков, папки карт, уведомления по онлайну карты, фильтр серверов, локальные уведомления, темы и переключение языка.",
            "Seguimiento de jugadores, carpetas de mapas, alertas de mapas, filtros de servidor, notificaciones locales, temas e interfaz multilingüe.");
        AboutDataTitleText.Text = Text("Data source", "Источник данных", "Fuente de datos");
        AboutDataText.Text = Text(
            "DDNetNW uses the public DDNet master server list. It does not log into DDNet and does not need a private server.",
            "DDNetNW использует публичный список master-серверов DDNet. Приложению не нужен вход в аккаунт и не нужен отдельный сервер.",
            "DDNetNW usa la lista pública de servidores master de DDNet. No inicia sesión en DDNet y no necesita servidor privado.");
        AboutStorageTitleText.Text = Text("Storage and privacy", "Хранение и приватность", "Almacenamiento y privacidad");
        AboutStorageText.Text = Text(
            "Settings are stored locally in the user AppData folder. The app does not send watched nicknames or maps to any third-party service.",
            "Настройки хранятся локально в папке AppData пользователя. Приложение не отправляет отслеживаемые ники или карты сторонним сервисам.",
            "La configuración se guarda localmente en AppData. La app no envía nicks o mapas vigilados a servicios de terceros.");
        AboutLimitationsTitleText.Text = Text("Limitations", "Ограничения", "Limitaciones");
        AboutLimitationsText.Text = Text(
            "DDNet nicknames are not accounts. The app tracks public nickname text and public map/server activity, so visually similar names can still be different players.",
            "Ники DDNet не являются аккаунтами. Приложение отслеживает публичный текст ника и активность карт/серверов, поэтому визуально похожие имена всё равно могут быть разными игроками.",
            "Los nicks de DDNet no son cuentas. La app sigue texto público de nicks y actividad pública de mapas/servidores, por eso nombres parecidos pueden ser jugadores diferentes.");

        UpdateMapServerFilterText();
        UpdateStaticUi();
        CooldownValueText.Text = IsRu ? $"{_cooldownSeconds} с" : IsEs ? $"{_cooldownSeconds} s" : $"{_cooldownSeconds} s";
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

    private bool IsRu => string.Equals(_currentLanguage, "ru", StringComparison.OrdinalIgnoreCase);
    private bool IsEs => string.Equals(_currentLanguage, "es", StringComparison.OrdinalIgnoreCase);

    private string Text(string en, string ru, string es)
    {
        return IsRu ? ru : IsEs ? es : en;
    }

    private string FormatSeconds(int seconds)
    {
        if (IsRu)
        {
            return $"{seconds} с";
        }

        if (IsEs)
        {
            return seconds == 1 ? "1 segundo" : $"{seconds} segundos";
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
        _searchDebounceTimer.Stop();
        _notificationService.Dispose();
        _masterClient.Dispose();
        base.OnClosed(e);
    }
}
