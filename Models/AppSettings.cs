namespace DDNetNW.Models;

public sealed class AppSettings
{
    public List<string> Nicknames { get; set; } = new();
    public int CooldownSeconds { get; set; } = 30;
    public bool WindowsNotificationsEnabled { get; set; } = true;
}
