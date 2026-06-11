using System;
using System.Drawing;
using System.IO;
using Forms = System.Windows.Forms;

namespace DDNetNW.Services;

public sealed class LocalNotificationService : IDisposable
{
    private readonly Forms.NotifyIcon _notifyIcon;

    public LocalNotificationService()
    {
        _notifyIcon = new Forms.NotifyIcon
        {
            Visible = true,
            Text = "DDNetNW"
        };

        try
        {
            var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "icon_app.ico");
            _notifyIcon.Icon = File.Exists(iconPath) ? new Icon(iconPath) : SystemIcons.Information;
        }
        catch
        {
            _notifyIcon.Icon = SystemIcons.Information;
        }
    }

    public void Show(string title, string message)
    {
        _notifyIcon.BalloonTipTitle = title;
        _notifyIcon.BalloonTipText = message;
        _notifyIcon.BalloonTipIcon = Forms.ToolTipIcon.Info;
        _notifyIcon.ShowBalloonTip(4000);
    }

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
    }
}
