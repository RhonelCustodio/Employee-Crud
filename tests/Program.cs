using EmployeeCrud.Data;
using EmployeeCrud.Models;
using Microsoft.Data.Sqlite;

var path = Path.Combine(Path.GetTempPath(), $"employee-crud-test-{Guid.NewGuid():N}.db");
var passed = 0;
void Check(bool condition, string name)
{
    if (!condition) throw new Exception($"FAIL: {name}");
    Console.WriteLine($"PASS: {name}");
    passed++;
}

try
{
    var repository = new EmployeeRepository(path);
    repository.Initialize();
    repository.Initialize();
    Check(repository.GetAll().Count == 0, "Initialize is repeatable; database starts empty");
    var id = repository.Create(new Employee
    {
        FullName = "Alex O'Brien", Email = "alex@example.com", Department = "Engineering"
    });
    var created = repository.GetAll().Single();
    Check(id > 0 && created.Id == id && created.FullName == "Alex O'Brien",
        "Create and read; quoted names are safe");
    var reopened = new EmployeeRepository(path);
    Check(reopened.GetAll().Single().Email == "alex@example.com", "Data persists across repository instances");
    Check(repository.Update(new Employee
    {
        Id = id, FullName = "Alex O'Brien", Email = "alex.new@example.com", Department = "Operations"
    }), "Update existing employee");
    Check(repository.GetAll().Single().Department == "Operations", "Read updated values");
    try
    {
        repository.Create(new Employee
        {
            FullName = "Duplicate", Email = "ALEX.NEW@EXAMPLE.COM", Department = "HR"
        });
        throw new Exception("Duplicate email was accepted");
    }
    catch (SqliteException ex) when (ex.SqliteExtendedErrorCode == 2067)
    {
        Check(true, "Reject duplicate email, ignoring case");
    }
    Check(!repository.Update(new Employee
    {
        Id = long.MaxValue, FullName = "Missing", Email = "missing@example.com", Department = "HR"
    }), "Updating a missing employee reports false");
    Check(repository.Delete(id), "Delete existing employee");
    Check(repository.GetAll().Count == 0, "Deleted employee is no longer returned");
    Check(!repository.Delete(id), "Deleting a missing employee reports false");
    Check(repository.GetDepartmentCounts().Count == 0, "Department summary is empty when no employees exist");
    repository.Create(new Employee
    {
        FullName = "Sam Lee", Email = "sam@example.com", Department = "Engineering"
    });
    repository.Create(new Employee
    {
        FullName = "Kim Lee", Email = "kim@example.com", Department = "engineering"
    });
    repository.Create(new Employee
    {
        FullName = "Jo Lee", Email = "jo@example.com", Department = "Engineering"
    });
    repository.Create(new Employee
    {
        FullName = "Pat Lee", Email = "pat@example.com", Department = "HR"
    });
    var counts = repository.GetDepartmentCounts();
    Check(counts.Count == 2, "Department summary groups names that differ only by case");
    Check(counts[0].Department == "Engineering" && counts[0].EmployeeCount == 3,
        "Larger department is first and keeps the more common spelling");
    Check(counts[1].Department == "HR" && counts[1].EmployeeCount == 1,
        "Smaller department count is correct");

    var admins = new AdminRepository(path);
    admins.Initialize();
    admins.Initialize();
    Check(!admins.HasAdmin(), "Admin table starts empty");
    try
    {
        admins.CreateAdmin("ab", "long-enough-password");
        throw new Exception("Short username was accepted");
    }
    catch (ArgumentException)
    {
        Check(true, "Reject a too-short admin username");
    }
    try
    {
        admins.CreateAdmin("owner", "short");
        throw new Exception("Short password was accepted");
    }
    catch (ArgumentException)
    {
        Check(true, "Reject a too-short admin password");
    }
    Check(admins.CreateAdmin("owner", "correct-horse-battery"), "Create the only admin");
    Check(admins.HasAdmin(), "Admin exists after creation");
    Check(!admins.CreateAdmin("other", "another-valid-password"), "A second admin is rejected");
    Check(admins.ValidateLogin("Owner", "correct-horse-battery"), "Login ignores username letter case");
    Check(!admins.ValidateLogin("owner", "wrong-password-value"), "Wrong password is rejected");
    Check(!admins.ValidateLogin("missing", "correct-horse-battery"), "Missing username is rejected");
    Console.WriteLine($"\nAll {passed} checks passed.");
}
finally
{
    SqliteConnection.ClearAllPools();
    if (File.Exists(path)) File.Delete(path);
}
