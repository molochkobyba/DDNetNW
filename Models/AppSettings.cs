using System.Collections.Generic;

namespace DDNetNW.Models;

public sealed class AppSettings
{
    public int SchemaVersion { get; set; } = 120;

    public List<string> Nicknames { get; set; } = new();
    public List<string> WatchedMaps { get; set; } = new();

    public int CooldownSeconds { get; set; } = 30;
    public bool WindowsNotificationsEnabled { get; set; } = true;
    public bool NotifyOnJoin { get; set; } = true;
    public bool NotifyOnLeave { get; set; } = true;
    public bool NotifyOnServerChange { get; set; } = true;
    public bool NotifyOnMapChange { get; set; } = true;
    public bool PlayNotificationSound { get; set; } = true;

    public bool MapAlertsEnabled { get; set; } = true;
    public int MapAlertMinPlayers { get; set; } = 8;
    public string MapServerFilter { get; set; } = "Any";

    public bool ShowServerAddress { get; set; } = false;
    public bool ShowExtraPlayerDetails { get; set; } = false;
    public string Language { get; set; } = "en";
    public string Theme { get; set; } = "dark";
}
