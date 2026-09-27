using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using EmployeeCrud.Data;

namespace EmployeeCrud;

public partial class LoginWindow : Window
{
    private readonly AdminRepository _admins;
    private bool _setup;
    private bool _busy;

    public LoginWindow(AdminRepository admins)
    {
        _admins = admins;
        InitializeComponent();
        if (_admins.HasAdmin())
            ShowLoginMode();
        else
            ShowSetupMode();
        UsernameBox.Focus();
    }

    private void ShowSetupMode()
    {
        _setup = true;
        Title = "Create admin account";
        TitleText.Text = "Create admin account";
        SubtitleText.Text = "This computer does not have an admin yet. Create one to open the employee directory.";
        ConfirmLabel.Visibility = Visibility.Visible;
        ConfirmInput.Visibility = Visibility.Visible;
        ActionButton.Content = "_Create admin account";
        HintText.Text = "Username: 3–50 letters, numbers, dots, underscores, or hyphens. Password: 12–128 characters. It is stored as a salted hash, never as plain text.";
        ErrorText.Text = "";
    }

    private void ShowLoginMode(string? message = null)
    {
        _setup = false;
        Title = "Admin login";
        TitleText.Text = "Admin login";
        SubtitleText.Text = "Sign in to open the employee directory.";
        ConfirmLabel.Visibility = Visibility.Collapsed;
        ConfirmInput.Visibility = Visibility.Collapsed;
        ConfirmInput.Clear();
        ActionButton.Content = "_Log in";
        HintText.Text = "Closing this window exits the app. Employee records stay saved on this device.";
        ErrorText.Text = message ?? "";
    }

    private async void ActionButton_Click(object sender, RoutedEventArgs e)
    {
        if (_busy) return;
        var username = UsernameBox.Text.Trim();
        var password = PasswordInput.Password;
        ErrorText.Text = "";

        if (_setup)
        {
            if (!AdminRepository.IsValidUsername(username))
            {
                ErrorText.Text = "Username must be 3–50 characters and use only letters, numbers, dots, underscores, or hyphens.";
                UsernameBox.Focus();
                return;
            }
            if (!AdminRepository.IsValidPassword(password))
            {
                ErrorText.Text = "Password must be 12–128 characters and cannot be blank.";
                PasswordInput.Focus();
                return;
            }
            if (!string.Equals(password, ConfirmInput.Password, StringComparison.Ordinal))
            {
                ErrorText.Text = "The passwords do not match.";
                ConfirmInput.Focus();
                return;
            }
        }
        else if (username.Length == 0 || password.Length == 0)
        {
            ErrorText.Text = "Enter your username and password.";
            return;
        }

        SetBusy(true);
        try
        {
            if (_setup)
            {
                var created = await Task.Run(() => _admins.CreateAdmin(username, password));
                PasswordInput.Clear();
                ConfirmInput.Clear();
                if (!created)
                {
                    ShowLoginMode("An admin account already exists. Log in to continue.");
                    return;
                }
                ShowLoginMode("Admin account created. Log in to continue.");
                UsernameBox.Text = username;
                PasswordInput.Focus();
                return;
            }

            var valid = await Task.Run(() => _admins.ValidateLogin(username, password));
            if (!valid)
            {
                PasswordInput.Clear();
                ErrorText.Text = "The username or password is incorrect.";
                await Task.Delay(1000);
                PasswordInput.Focus();
                return;
            }

            PasswordInput.Clear();
            SetBusy(false);
            DialogResult = true;
        }
        catch (Exception ex)
        {
            PasswordInput.Clear();
            ConfirmInput.Clear();
            ErrorText.Text = _setup
                ? $"Could not create the admin account. {ex.Message}"
                : "Could not sign in. Check that the database is available, then try again.";
        }
        finally
        {
            if (DialogResult != true)
                SetBusy(false);
        }
    }

    private void SetBusy(bool busy)
    {
        _busy = busy;
        UsernameBox.IsEnabled = !busy;
        PasswordInput.IsEnabled = !busy;
        ConfirmInput.IsEnabled = !busy;
        ActionButton.IsEnabled = !busy;
        Cursor = busy ? Cursors.Wait : null;
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (_busy) e.Cancel = true;
        base.OnClosing(e);
    }
}
