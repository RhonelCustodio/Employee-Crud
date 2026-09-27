using System.ComponentModel;
using System.Net.Mail;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using EmployeeCrud.Data;
using EmployeeCrud.Models;
using Microsoft.Data.Sqlite;

namespace EmployeeCrud;

public partial class MainWindow : Window
{
    private readonly EmployeeRepository _repository;
    private ICollectionView? _employeesView;
    private int _totalCount;
    private long? _editingId;

    public MainWindow(EmployeeRepository repository)
    {
        _repository = repository;
        InitializeComponent();
        ReloadEmployees();
    }

    private void ReloadEmployees()
    {
        var employees = _repository.GetAll();
        _totalCount = employees.Count;
        _employeesView = CollectionViewSource.GetDefaultView(employees);
        _employeesView.Filter = item => MatchesSearch((Employee)item);
        EmployeesGrid.ItemsSource = _employeesView;
        UpdateCounts();
    }

    private bool MatchesSearch(Employee employee)
    {
        var query = SearchBox.Text.Trim();
        return employee.FullName.Contains(query, StringComparison.OrdinalIgnoreCase)
            || employee.Email.Contains(query, StringComparison.OrdinalIgnoreCase)
            || employee.Department.Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    private void UpdateCounts()
    {
        var visible = _employeesView?.Cast<Employee>().Count() ?? 0;
        CountText.Text = $"{visible} of {_totalCount} employees";
        EmptyText.Visibility = visible == 0 ? Visibility.Visible : Visibility.Collapsed;
        EmptyText.Text = _totalCount == 0
            ? "No employees yet. Add your first team member →"
            : "No matches. Try another search.";
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_employeesView is null) return;
        _employeesView.Refresh();
        UpdateCounts();
    }

    private void EmployeesGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (EmployeesGrid.SelectedItem is Employee employee)
            EditEmployee(employee);
    }

    private void EditEmployee(Employee employee)
    {
        _editingId = employee.Id;
        NameBox.Text = employee.FullName;
        EmailBox.Text = employee.Email;
        DepartmentBox.Text = employee.Department;
        EditorTitle.Text = "Edit employee";
        EditorSubtitle.Text = $"Employee #{employee.Id} · All fields required.";
        SaveButton.Content = "_Save changes";
        DeleteButton.IsEnabled = true;
        ValidationText.Text = "";
    }

    private void ClearEditor()
    {
        _editingId = null;
        EmployeesGrid.SelectedItem = null;
        NameBox.Clear();
        EmailBox.Clear();
        DepartmentBox.Clear();
        EditorTitle.Text = "New employee";
        EditorSubtitle.Text = "All fields are required.";
        SaveButton.Content = "_Add employee";
        DeleteButton.IsEnabled = false;
        ValidationText.Text = "";
    }

    private Employee? ReadForm()
    {
        var name = NameBox.Text.Trim();
        var email = EmailBox.Text.Trim();
        var department = DepartmentBox.Text.Trim();
        ValidationText.Text = "";
        if (string.IsNullOrWhiteSpace(name))
        {
            ValidationText.Text = "Enter the employee’s full name.";
            NameBox.Focus();
            return null;
        }
        if (!MailAddress.TryCreate(email, out var address)
            || !string.Equals(address.Address, email, StringComparison.OrdinalIgnoreCase))
        {
            ValidationText.Text = "Enter a valid email address, such as name@example.com.";
            EmailBox.Focus();
            return null;
        }
        if (string.IsNullOrWhiteSpace(department))
        {
            ValidationText.Text = "Enter a department.";
            DepartmentBox.Focus();
            return null;
        }
        return new Employee
        {
            Id = _editingId ?? 0,
            FullName = name,
            Email = email,
            Department = department
        };
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var employee = ReadForm();
        if (employee is null) return;
        var updating = _editingId.HasValue;
        long id;
        try
        {
            if (updating)
            {
                if (!_repository.Update(employee))
                {
                    ValidationText.Text = "This employee no longer exists. Refresh the directory.";
                    return;
                }
                id = employee.Id;
            }
            else
            {
                id = _repository.Create(employee);
            }
        }
        catch (SqliteException ex) when (ex.SqliteExtendedErrorCode == 2067)
        {
            ValidationText.Text = "That email address already belongs to an employee.";
            EmailBox.Focus();
            return;
        }
        catch (Exception ex)
        {
            ShowDatabaseError("save this employee", ex);
            return;
        }

        // The write is already committed. Clear the form before refreshing so a
        // failed refresh never encourages accidentally submitting a second insert.
        ClearEditor();
        StatusText.Text = updating ? $"Employee #{id} updated." : $"Employee #{id} added.";
        try
        {
            SearchBox.Clear();
            ReloadEmployees();
            NameBox.Focus();
        }
        catch (Exception ex)
        {
            ShowDatabaseError("refresh the directory (your changes were saved)", ex);
        }
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (_editingId is not long id) return;
        if (MessageBox.Show(this,
                $"Permanently delete employee #{id}?\n\nThis cannot be undone.",
                "Delete employee", MessageBoxButton.YesNo, MessageBoxImage.Warning,
                MessageBoxResult.No) != MessageBoxResult.Yes) return;
        try
        {
            var deleted = _repository.Delete(id);
            ClearEditor();
            StatusText.Text = deleted ? $"Employee #{id} deleted." : "This employee was already removed.";
        }
        catch (Exception ex)
        {
            ShowDatabaseError("delete this employee", ex);
            return;
        }
        try { ReloadEmployees(); }
        catch (Exception ex) { ShowDatabaseError("refresh the directory (deletion completed)", ex); }
    }

    private void Logout_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show(this,
                "Log out of the admin account?\n\nUnsaved form changes will be discarded. Saved employees stay in the database.",
                "Log out", MessageBoxButton.YesNo, MessageBoxImage.Question,
                MessageBoxResult.No) != MessageBoxResult.Yes) return;
        ((App)Application.Current).Logout();
    }

    private void Refresh_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            ReloadEmployees();
            ClearEditor();
            StatusText.Text = "Directory refreshed. Form cleared.";
        }
        catch (Exception ex) { ShowDatabaseError("refresh the directory", ex); }
    }

    private void ShowDatabaseError(string action, Exception exception)
    {
        StatusText.Text = $"Could not {action}.";
        MessageBox.Show(this, $"Could not {action}.\n\n{exception.Message}",
            "Database error", MessageBoxButton.OK, MessageBoxImage.Error);
    }
}
