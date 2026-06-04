using System.Windows;
using System.Windows.Controls;

namespace MyApp;

/// <summary>
/// A small code-built modal password dialog for the WPF app.
/// Show() returns the entered text, or null if the user cancels.
/// </summary>
internal static class PasswordPrompt
{
    public static string? Show(Window owner, string prompt)
    {
        var dialog = new Window
        {
            Title = "Master Password",
            Width = 360,
            Height = 170,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = owner,
            ResizeMode = ResizeMode.NoResize,
            WindowStyle = WindowStyle.ToolWindow,
        };

        var panel = new StackPanel { Margin = new Thickness(16) };

        panel.Children.Add(new TextBlock
        {
            Text = prompt,
            Margin = new Thickness(0, 0, 0, 10),
            TextWrapping = TextWrapping.Wrap,
        });

        var passwordBox = new PasswordBox { Margin = new Thickness(0, 0, 0, 16) };
        panel.Children.Add(passwordBox);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
        };

        string? result = null;

        var ok = new Button { Content = "OK", Width = 80, Margin = new Thickness(0, 0, 8, 0), IsDefault = true };
        ok.Click += (s, e) => { result = passwordBox.Password; dialog.DialogResult = true; };

        var cancel = new Button { Content = "Cancel", Width = 80, IsCancel = true };

        buttons.Children.Add(ok);
        buttons.Children.Add(cancel);
        panel.Children.Add(buttons);

        dialog.Content = panel;
        passwordBox.Loaded += (s, e) => passwordBox.Focus();

        bool? dr = dialog.ShowDialog();
        return dr == true ? result : null;
    }
}
