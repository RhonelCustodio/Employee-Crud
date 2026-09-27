namespace EmployeeCrud.Models;

public sealed class Employee
{
    public long Id { get; init; }
    public string FullName { get; init; } = "";
    public string Email { get; init; } = "";
    public string Department { get; init; } = "";
}
