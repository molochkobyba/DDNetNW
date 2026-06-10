using System.Windows;
using System.Windows.Input;

namespace DDNetNW;

/// <summary>
/// Dialog used to add a new tracked DDNet nickname.
/// Originally created by molochko.
/// </summary>
public partial class AddNicknameWindow : Window
{
    public string Nickname { get; private set; } = string.Empty;

    public AddNicknameWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => NicknameBox.Focus();
        NicknameBox.KeyDown += NicknameBox_KeyDown;
    }

    private void NicknameBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            TryAdd();
        }
    }

    private void Add_Click(object sender, RoutedEventArgs e) => TryAdd();

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void TryAdd()
    {
        var value = NicknameBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(value))
        {
            ErrorText.Text = "Nickname cannot be empty.";
            return;
        }

        Nickname = value;
        DialogResult = true;
        Close();
    }
}
