using Avalonia.Controls;
using Avalonia.Interactivity;

namespace FeedMe.App.Views;

public partial class CategoryPrompt : Window
{
    public CategoryPrompt()
    {
        InitializeComponent();
    }

    public CategoryPrompt(string? initialCategory) : this()
    {
        Input.Text = initialCategory ?? string.Empty;
    }

    private void OnOk(object? sender, RoutedEventArgs e) => Close(Input.Text?.Trim() ?? string.Empty);

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(null);
}
