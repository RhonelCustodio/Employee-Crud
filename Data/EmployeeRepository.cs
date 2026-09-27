using EmployeeCrud.Models;
using Microsoft.Data.Sqlite;

namespace EmployeeCrud.Data;

/// <summary>Each operation owns its connection. All user values use SQL parameters.</summary>
public sealed class EmployeeRepository
{
    private readonly string _connectionString;

    public EmployeeRepository(string databasePath)
    {
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            DefaultTimeout = 5
        }.ToString();
    }

    private SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection(_connectionString);
        try
        {
            connection.Open();
            return connection;
        }
        catch
        {
            connection.Dispose();
            throw;
        }
    }

    public void Initialize()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS Employees (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                FullName TEXT NOT NULL CHECK(length(trim(FullName)) BETWEEN 1 AND 100),
                Email TEXT NOT NULL COLLATE NOCASE UNIQUE
                    CHECK(length(trim(Email)) BETWEEN 3 AND 254),
                Department TEXT NOT NULL CHECK(length(trim(Department)) BETWEEN 1 AND 100)
            );
            """;
        command.ExecuteNonQuery();
    }

    public List<Employee> GetAll()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Id, FullName, Email, Department FROM Employees
            ORDER BY FullName COLLATE NOCASE, Id;
            """;
        using var reader = command.ExecuteReader();
        var employees = new List<Employee>();
        while (reader.Read())
        {
            employees.Add(new Employee
            {
                Id = reader.GetInt64(0),
                FullName = reader.GetString(1),
                Email = reader.GetString(2),
                Department = reader.GetString(3)
            });
        }
        return employees;
    }

    /// <summary>
    /// Counts employees per department. Names that differ only by letter case are one department.
    /// The displayed spelling is the one used by the most employees.
    /// </summary>
    public IReadOnlyList<DepartmentCount> GetDepartmentCounts()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Department, COUNT(*)
            FROM Employees
            GROUP BY Department
            ORDER BY COUNT(*) DESC, Department COLLATE NOCASE;
            """;
        using var reader = command.ExecuteReader();
        var raw = new List<(string Department, int Count)>();
        while (reader.Read())
            raw.Add((reader.GetString(0), checked((int)reader.GetInt64(1))));

        return raw
            .GroupBy(row => row.Department.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var spelling = group
                    .GroupBy(row => row.Department)
                    .OrderByDescending(spellingGroup => spellingGroup.Sum(row => row.Count))
                    .ThenBy(spellingGroup => spellingGroup.Key, StringComparer.Ordinal)
                    .First()
                    .Key;
                return new DepartmentCount
                {
                    Department = spelling,
                    EmployeeCount = group.Sum(row => row.Count)
                };
            })
            .OrderByDescending(item => item.EmployeeCount)
            .ThenBy(item => item.Department, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public long Create(Employee employee)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Employees (FullName, Email, Department)
            VALUES ($name, $email, $department)
            RETURNING Id;
            """;
        AddParameters(command, employee);
        return (long)command.ExecuteScalar()!;
    }

    public bool Update(Employee employee)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE Employees
            SET FullName = $name, Email = $email, Department = $department
            WHERE Id = $id;
            """;
        AddParameters(command, employee);
        command.Parameters.AddWithValue("$id", employee.Id);
        return command.ExecuteNonQuery() == 1;
    }

    public bool Delete(long id)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Employees WHERE Id = $id;";
        command.Parameters.AddWithValue("$id", id);
        return command.ExecuteNonQuery() == 1;
    }

    private static void AddParameters(SqliteCommand command, Employee employee)
    {
        command.Parameters.AddWithValue("$name", employee.FullName);
        command.Parameters.AddWithValue("$email", employee.Email);
        command.Parameters.AddWithValue("$department", employee.Department);
    }
}
