using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;

namespace EmployeeCrud.Data;

/// <summary>One local admin account. Passwords are stored only as PBKDF2-SHA256 hashes.</summary>
public sealed class AdminRepository
{
    public const int PasswordIterations = 600_000;
    private const int SaltSize = 16;
    private const int HashSize = 32;
    private static readonly Regex UsernamePattern = new(@"\A[A-Za-z0-9_.-]{3,50}\z", RegexOptions.CultureInvariant);

    private readonly string _connectionString;

    public AdminRepository(string databasePath)
    {
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            DefaultTimeout = 5
        }.ToString();
    }

    public static bool IsValidUsername(string username) => UsernamePattern.IsMatch(username);

    public static bool IsValidPassword(string password) =>
        password.Length is >= 12 and <= 128 && !string.IsNullOrWhiteSpace(password);

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
            CREATE TABLE IF NOT EXISTS Admins (
                Id INTEGER PRIMARY KEY CHECK(Id = 1),
                Username TEXT NOT NULL COLLATE NOCASE UNIQUE,
                PasswordHash BLOB NOT NULL,
                PasswordSalt BLOB NOT NULL,
                PasswordIterations INTEGER NOT NULL
            );
            """;
        command.ExecuteNonQuery();
    }

    public bool HasAdmin()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT EXISTS(SELECT 1 FROM Admins);";
        return Convert.ToInt64(command.ExecuteScalar()) == 1;
    }

    /// <summary>Creates the only admin. Returns false when an admin already exists.</summary>
    public bool CreateAdmin(string username, string password)
    {
        username = username.Trim();
        if (!IsValidUsername(username))
            throw new ArgumentException("Username must be 3–50 characters and use only letters, numbers, dots, underscores, or hyphens.");
        if (!IsValidPassword(password))
            throw new ArgumentException("Password must be 12–128 characters and cannot be blank.");
        if (HasAdmin()) return false;

        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = HashPassword(password, salt, PasswordIterations);
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Admins (Id, Username, PasswordHash, PasswordSalt, PasswordIterations)
            VALUES (1, $username, $hash, $salt, $iterations);
            """;
        command.Parameters.AddWithValue("$username", username);
        command.Parameters.Add("$hash", SqliteType.Blob).Value = hash;
        command.Parameters.Add("$salt", SqliteType.Blob).Value = salt;
        command.Parameters.AddWithValue("$iterations", PasswordIterations);
        try
        {
            return command.ExecuteNonQuery() == 1;
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 19)
        {
            return false;
        }
    }

    public bool ValidateLogin(string username, string password)
    {
        username = username.Trim();
        byte[]? storedHash = null;
        byte[]? salt = null;
        var iterations = PasswordIterations;

        using (var connection = OpenConnection())
        using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                SELECT PasswordHash, PasswordSalt, PasswordIterations
                FROM Admins
                WHERE Username = $username
                LIMIT 1;
                """;
            command.Parameters.AddWithValue("$username", username);
            using var reader = command.ExecuteReader();
            if (reader.Read())
            {
                storedHash = (byte[])reader.GetValue(0);
                salt = (byte[])reader.GetValue(1);
                iterations = reader.GetInt32(2);
            }
        }

        if (iterations is < 1 or > 2_000_000)
            return false;

        var computed = HashPassword(password, salt ?? new byte[SaltSize], iterations);
        return storedHash is not null
            && salt is not null
            && CryptographicOperations.FixedTimeEquals(storedHash, computed);
    }

    private static byte[] HashPassword(string password, byte[] salt, int iterations) =>
        Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, HashSize);
}
