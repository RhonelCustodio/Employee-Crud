# Employee CRUD — C# WPF + SQLite

A Windows desktop app for managing employees. It uses a local SQLite database, an admin login stored in that database, and can be opened in Visual Studio or Visual Studio Code. No SQL Server installation is needed.

## Features

- **Admin setup and login:** the first launch creates one admin account. Later launches require that login before the directory opens. The password is stored as a PBKDF2-SHA256 hash with a random salt, never as plain text.
- **Logout:** the admin can log out and return to a fresh login window. Closing the login window or the directory exits the app. Saved employees remain in the database.
- **Create:** add an employee with name, email, and department. A successful save clears the form automatically.
- **Read:** display saved employees in a sortable table; search all three fields.
- **Update:** select a row, edit its details, and save changes. The form clears after a successful update too.
- **Delete:** remove an employee after a confirmation prompt.
- Required-field and email-format validation; case-insensitive unique email addresses.
- Parameterized SQL and automatic local database creation.

## Download (Windows)

The easiest way to try the app: open this repository's [Releases](../../releases) page, download `EmployeeCrud-win-x64.zip`, extract it anywhere, and run `EmployeeCrud.exe`. The build is self-contained, so .NET does not need to be installed. It runs on 64-bit Windows 10 and 11.

To build from source instead, continue below.

## 1. Install prerequisites (Windows)

1. Install [Visual Studio 2022 or newer](https://visualstudio.microsoft.com/) with the **.NET desktop development** workload, or install Visual Studio Code plus the **.NET 9 SDK**.
2. The project targets `net9.0-windows`. Install the .NET 9 SDK if Visual Studio does not already include it: [https://dotnet.microsoft.com/download/dotnet/9.0](https://dotnet.microsoft.com/download/dotnet/9.0).
3. Check a terminal:

```powershell
dotnet --version
```

The version should be `9.0.xxx` or newer.

> WPF is Windows-only. macOS, Linux, WSL, and browser previews cannot run this window.

## 2. Open and run in Visual Studio

1. Extract `workspace.zip`.
2. Open `EmployeeCrud\EmployeeCrud.csproj` in Visual Studio. You do not need a separate solution file.
3. Wait for the NuGet restore to finish. The first restore needs internet access.
4. Press **F5**.

The first run asks you to create an admin account. After that, the same database asks you to log in.

## 3. Try the workflow

1. Create an admin username such as `owner` and a password of at least 12 characters. Confirm the password, then click **Create admin account**.
2. Log in with that same username and password. Letter case in the username does not matter.
3. Enter `Alex Santos`, `alex@example.com`, and `Engineering`, then click **Add employee**. The form clears by itself. There is no New button.
4. Select the saved row, change the department to `Operations`, and click **Save changes**. The form clears again.
5. Search by name, email, or department.
6. Select a row and click **Delete employee**. Choose **No** to cancel or **Yes** to permanently delete.
7. Click **Log out**. Choose **No** to stay, or **Yes** to return to the login window. Unsaved form text is discarded. Saved employees remain.
8. Close and reopen the app. The admin login appears again, and the employee records are still there.

**Form behavior:** selecting a row fills the form for editing. A successful add or update clears the form and the search box, then puts the cursor back in Full name. Refresh also clears the form. A validation error keeps what you typed.

## Database location

The app creates the database and tables automatically at:

```text
%LOCALAPPDATA%\EmployeeCrud\employees.db
```

For example: `C:\Users\YourName\AppData\Local\EmployeeCrud\employees.db`.

Both `Employees` and `Admins` live in that file. Data survives rebuilding or moving the project. To reset the app, close every instance and delete this file. The next launch asks for a new admin account.

SQLite data is **not encrypted**. Anyone who can open the database file can read employee records and can replace the admin hash. This login protects the window, not the file. Use Windows account permissions for real employee information. A shared or production system should enforce login on a server.

There is no hardcoded admin password, no password-reset screen, and no lockout after repeated failures. A failed login waits one second and shows a generic error.

## Project layout

```text
EmployeeCrud/
├── EmployeeCrud.csproj          .NET 9 WPF project and SQLite dependencies
├── App.xaml                    Shared resources; no StartupUri
├── App.xaml.cs                 Database startup, login, and logout
├── LoginWindow.xaml            First-time admin setup and login
├── LoginWindow.xaml.cs
├── MainWindow.xaml             Directory, search, and editor
├── MainWindow.xaml.cs          CRUD events, filtering, validation, logout
├── Models/
│   ├── Employee.cs             Employee data model
│   └── DepartmentCount.cs      Department name and employee count
├── Data/
│   ├── EmployeeRepository.cs   Parameterized employee commands
│   └── AdminRepository.cs      One admin account and password hash
├── Styles/
│   └── Controls.xaml           Shared control styles
├── .vscode/                    Optional Visual Studio Code launch files
└── tests/
    ├── RepositorySmokeTests.csproj
    └── Program.cs              Database behavior checks
```

## How it works

`App.OnStartup` creates the local data folder, initializes both tables, and shows the login window. A successful login opens a new main window. Logout switches shutdown mode before closing that window, then shows a fresh login window.

| Operation | Repository method | SQL |
|---|---|---|
| Create | `Create(employee)` | `INSERT ... RETURNING Id` |
| Read | `GetAll()` | `SELECT ... ORDER BY FullName` |
| Update | `Update(employee)` | `UPDATE ... WHERE Id = $id` |
| Delete | `Delete(id)` | `DELETE ... WHERE Id = $id` |
| Admin setup | `CreateAdmin(username, password)` | `INSERT INTO Admins ...` |
| Admin login | `ValidateLogin(username, password)` | `SELECT` the hash, then compare it in code |

All user values are passed as SQL parameters. Each operation disposes its connection and command. The admin password uses PBKDF2-SHA256, 600,000 iterations, a random 16-byte salt, and a 32-byte hash. Comparison uses `CryptographicOperations.FixedTimeEquals`.

This version uses **WPF code-behind plus separate repositories**, not MVVM. It loads the directory into memory and makes short synchronous employee queries on the UI thread. Login hashing runs on a background thread.

## Run the database tests

```powershell
dotnet run --project .\tests\RepositorySmokeTests.csproj
```

These tests use a temporary database, never the application's real employee database. They cover employee CRUD, department grouping, admin creation, rejection of a second admin, and login success and failure.

**Verification performed:** the WPF project cross-compiled with zero warnings and zero errors, and all 23 repository checks passed. The windows themselves were not visually tested because this environment is Linux. On Windows, press F5 to confirm the login window and directory.

## Optional: publish a standalone Windows executable

For 64-bit Intel/AMD Windows:

```powershell
dotnet publish .\EmployeeCrud.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

Find the executable under `bin\Release\net9.0-windows\win-x64\publish\`. The executable is larger because it includes the .NET runtime. For Windows ARM64, change the runtime identifier to `win-arm64`.

## Troubleshooting

- **`dotnet` is not recognized:** install the Windows .NET SDK and reopen Visual Studio.
- **The SDK does not support .NET 9:** install the .NET 9 SDK; check `dotnet --list-sdks`.
- **The login window never appears:** stop the debugger, rebuild, and press F5 again. The app no longer opens the directory directly.
- **Duplicate email message:** emails must be unique, ignoring letter case.
- **Database locked/unavailable:** close any external SQLite editor, including the Database Client connection, then retry.
- **XAML changes are not visible:** stop the app and rebuild before pressing F5 again.
- **Forgot the admin password:** close the app and delete `%LOCALAPPDATA%\EmployeeCrud\employees.db`. This also deletes the employee list.

## Dependencies

- `Microsoft.Data.Sqlite` 10.0.9
- `SQLitePCLRaw.bundle_e_sqlite3` 3.0.5, explicitly pinned to use an updated native SQLite bundle rather than the older transitive bundle.

Review dependency updates and vulnerability reports periodically (`dotnet list package --vulnerable --include-transitive`).
