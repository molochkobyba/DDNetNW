using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace DDNetNW;

public partial class SelectRegionsWindow : Window
{
    private bool _suppressEvents;

    public string LanguageCode { get; set; } = "en";
    public string SelectedFilter { get; set; } = "Any";

    public SelectRegionsWindow()
    {
        InitializeComponent();
    }

    protected override void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e);
        ApplyLanguage();
        LoadFilter();
    }

    private void ApplyLanguage()
    {
        var ru = string.Equals(LanguageCode, "ru", StringComparison.OrdinalIgnoreCase);
        var es = string.Equals(LanguageCode, "es", StringComparison.OrdinalIgnoreCase);
        string T(string en, string ruText, string esText) => ru ? ruText : es ? esText : en;

        Title = T("Select regions", "Выбор регионов", "Seleccionar regiones");
        TitleText.Text = T("Tracked server regions", "Отслеживаемые регионы серверов", "Regiones de servidores");
        SubtitleText.Text = T(
            "Choose where map alerts should look for active servers.",
            "Выберите, где уведомления по картам будут искать активные серверы.",
            "Elige dónde buscar servidores activos para las alertas de mapas.");
        AnyCheck.Content = T("Any region", "Любой регион", "Cualquier región");
        CancelButton.Content = T("Cancel", "Отмена", "Cancelar");
        SaveButton.Content = T("Save", "Сохранить", "Guardar");
    }

    private void LoadFilter()
    {
        _suppressEvents = true;
        try
        {
            var regions = ParseFilter(SelectedFilter).ToHashSet(StringComparer.OrdinalIgnoreCase);
            AnyCheck.IsChecked = regions.Count == 0;
            GerCheck.IsChecked = regions.Contains("GER");
            RusCheck.IsChecked = regions.Contains("RUS");
            PolCheck.IsChecked = regions.Contains("POL");
            FraCheck.IsChecked = regions.Contains("FRA");
            UsaCheck.IsChecked = regions.Contains("USA");
            BraCheck.IsChecked = regions.Contains("BRA");
            SetRegionChecksEnabled(AnyCheck.IsChecked != true);
        }
        finally
        {
            _suppressEvents = false;
        }
    }

    private void AnyCheck_Changed(object sender, RoutedEventArgs e)
    {
        if (_suppressEvents)
        {
            return;
        }

        var any = AnyCheck.IsChecked == true;
        SetRegionChecksEnabled(!any);

        if (any)
        {
            _suppressEvents = true;
            GerCheck.IsChecked = false;
            RusCheck.IsChecked = false;
            PolCheck.IsChecked = false;
            FraCheck.IsChecked = false;
            UsaCheck.IsChecked = false;
            BraCheck.IsChecked = false;
            _suppressEvents = false;
        }
    }

    private void RegionCheck_Changed(object sender, RoutedEventArgs e)
    {
        if (_suppressEvents)
        {
            return;
        }

        if (AnyCheck.IsChecked == true)
        {
            _suppressEvents = true;
            AnyCheck.IsChecked = false;
            SetRegionChecksEnabled(true);
            _suppressEvents = false;
        }
    }

    private void SetRegionChecksEnabled(bool enabled)
    {
        GerCheck.IsEnabled = enabled;
        RusCheck.IsEnabled = enabled;
        PolCheck.IsEnabled = enabled;
        FraCheck.IsEnabled = enabled;
        UsaCheck.IsEnabled = enabled;
        BraCheck.IsEnabled = enabled;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var selected = new List<string>();
        AddIfChecked(selected, GerCheck, "GER");
        AddIfChecked(selected, RusCheck, "RUS");
        AddIfChecked(selected, PolCheck, "POL");
        AddIfChecked(selected, FraCheck, "FRA");
        AddIfChecked(selected, UsaCheck, "USA");
        AddIfChecked(selected, BraCheck, "BRA");

        SelectedFilter = AnyCheck.IsChecked == true || selected.Count == 0
            ? "Any"
            : string.Join(",", selected);

        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private static void AddIfChecked(ICollection<string> selected, CheckBox checkBox, string region)
    {
        if (checkBox.IsChecked == true)
        {
            selected.Add(region);
        }
    }

    private static IEnumerable<string> ParseFilter(string filter)
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
}
