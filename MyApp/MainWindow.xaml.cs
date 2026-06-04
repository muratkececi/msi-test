using System.Windows;

namespace MyApp;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        RefreshServiceStatus();
    }

    private void RefreshServiceStatus()
    {
        bool running = ServiceControlClient.IsRunning();
        ServiceStatusText.Text = running
            ? "Service status: running"
            : "Service status: stopped";
        StopServiceButton.IsEnabled = running;
        StartServiceButton.IsEnabled = !running;
    }

    private void StopService_Click(object sender, RoutedEventArgs e)
    {
        // Stopping the protected service requires the master password.
        string? password = PasswordPrompt.Show(this, "Enter the master password to stop the service:");
        if (password == null)
            return; // cancelled

        if (!ServiceControlClient.ValidatePassword(password))
        {
            MessageBox.Show(this, "Wrong password.", "MyApp",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (ServiceControlClient.RequestStop(out string? error))
            MessageBox.Show(this, "Service stopped.", "MyApp",
                MessageBoxButton.OK, MessageBoxImage.Information);
        else
            MessageBox.Show(this, "Could not stop the service: " + error, "MyApp",
                MessageBoxButton.OK, MessageBoxImage.Error);

        RefreshServiceStatus();
    }

    private void StartService_Click(object sender, RoutedEventArgs e)
    {
        // Starting is not password-protected (the deny ACE blocks only STOP).
        if (ServiceControlClient.Start(out string? error))
            MessageBox.Show(this, "Service started.", "MyApp",
                MessageBoxButton.OK, MessageBoxImage.Information);
        else
            MessageBox.Show(this, "Could not start the service: " + error, "MyApp",
                MessageBoxButton.OK, MessageBoxImage.Error);

        RefreshServiceStatus();
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
