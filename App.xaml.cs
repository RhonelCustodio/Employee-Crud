using System.IO;
using System.Windows;
using EmployeeCrud.Data;

namespace EmployeeCrud;

public partial class App : Application
{
    private EmployeeRepository? _employees;
    private AdminRepository? _admins;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        try
        {
            var folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "EmployeeCrud");
            Directory.CreateDirectory(folder);
            var databasePath = Path.Combine(folder, "employees.db");
            _employees = new EmployeeRepository(databasePath);
            _admins = new AdminRepository(databasePath);
            _employees.Initialize();
            _admins.Initialize();
            ShowLoginWindow();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not open the employee database.\n\n{ex.Message}",
                "Startup error", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    private void ShowLoginWindow()
    {
        var login = new LoginWindow(_admins!);
        MainWindow = login;
        var signedIn = login.ShowDialog() == true;
        if (!signedIn)
        {
            Shutdown();
            return;
        }

        var directory = new MainWindow(_employees!);
        MainWindow = directory;
        ShutdownMode = ShutdownMode.OnMainWindowClose;
        directory.Show();
    }

    public void Logout()
    {
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        MainWindow?.Close();
        ShowLoginWindow();
    }
}
