using System.Windows;
using System.Windows.Input;

namespace DDNetNW;

public partial class AddNicknameWindow : Window
{
    public string Nickname { get; private set; } = string.Empty;
    public string LanguageCode { get; set; } = "en";
    public bool IsEditMode { get; set; }
    public bool IsMapMode { get; set; }
    public string InitialNickname { get; set; } = string.Empty;

    public AddNicknameWindow()
    {
        InitializeComponent();
        Loaded += AddNicknameWindow_Loaded;
        NicknameBox.KeyDown += NicknameBox_KeyDown;
    }

    private void AddNicknameWindow_Loaded(object sender, RoutedEventArgs e)
    {
        ApplyLanguage();
        if (!string.IsNullOrWhiteSpace(InitialNickname))
        {
            NicknameBox.Text = InitialNickname;
        }
        NicknameBox.Focus();
        NicknameBox.SelectAll();
    }

    private void ApplyLanguage()
    {
        var isRu = LanguageCode == "ru";
        var objectName = IsMapMode ? (isRu ? "карту" : "map") : (isRu ? "ник" : "nickname");

        DialogTitleBarText.Text = isRu
            ? IsEditMode ? $"Изменить {objectName}" : $"Добавить {objectName}"
            : IsEditMode ? $"Edit {objectName}" : $"Add {objectName}";

        DialogHeaderText.Text = DialogTitleBarText.Text;
        DialogDescriptionText.Text = IsMapMode
            ? isRu ? "Введите точное название карты DDNet." : "Enter the exact DDNet map name."
            : isRu ? "Введите точный ник DDNet для отслеживания." : "Enter the exact DDNet nickname to watch.";

        NicknameLabelText.Text = IsMapMode ? (isRu ? "Карта" : "Map") : (isRu ? "Ник" : "Nickname");
        CancelButton.Content = isRu ? "Отмена" : "Cancel";
        SaveButton.Content = isRu ? (IsEditMode ? "Сохранить" : "Добавить") : (IsEditMode ? "Save" : "Add");
        Title = $"DDNetNW - {DialogTitleBarText.Text}";
    }

    private void NicknameBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            TrySubmit();
        }
        else if (e.Key == Key.Escape)
        {
            Cancel_Click(sender, new RoutedEventArgs());
        }
    }

    private void Add_Click(object sender, RoutedEventArgs e) => TrySubmit();

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void TrySubmit()
    {
        var value = NicknameBox.Text.Trim();
        var isRu = LanguageCode == "ru";

        if (string.IsNullOrWhiteSpace(value))
        {
            ErrorText.Text = IsMapMode
                ? isRu ? "Название карты не может быть пустым." : "Map name cannot be empty."
                : isRu ? "Ник не может быть пустым." : "Nickname cannot be empty.";
            return;
        }

        ErrorText.Text = string.Empty;
        Nickname = value;
        DialogResult = true;
        Close();
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }
}
