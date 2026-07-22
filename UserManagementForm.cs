using System.Text.Json;
using CIEL.Reconciliation.Models;

namespace CIEL.Reconciliation.Security;

public sealed class UserStore
{
    private readonly object _sync = new();
    private readonly string _folder;
    private readonly string _usersFile;
    private readonly string _auditFile;
    private readonly JsonSerializerOptions _json = new() { WriteIndented = true };
    private List<AppUser> _users = new();

    public UserStore()
    {
        _folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CIEL Reconciliation");
        _usersFile = Path.Combine(_folder, "users.json");
        _auditFile = Path.Combine(_folder, "audit.log");
        Directory.CreateDirectory(_folder);
        Load();
    }

    public bool HasUsers { get { lock (_sync) return _users.Count > 0; } }

    public IReadOnlyList<AppUser> GetUsers()
    {
        lock (_sync) return _users.Select(Clone).OrderBy(x => x.Username).ToList();
    }

    public AppUser? Authenticate(string username, string password)
    {
        lock (_sync)
        {
            var user = _users.FirstOrDefault(x => string.Equals(x.Username, username.Trim(), StringComparison.OrdinalIgnoreCase));
            if (user is null || !user.IsActive || !PasswordHasher.Verify(password, user.PasswordHash, user.PasswordSalt))
            {
                Audit(username.Trim(), "Failed login", "Invalid credentials or inactive account");
                return null;
            }
            user.LastLoginUtc = DateTime.UtcNow;
            Save();
            Audit(user.Username, "Login", "Successful login");
            return Clone(user);
        }
    }

    public void CreateInitialAdministrator(string password)
    {
        lock (_sync)
        {
            if (_users.Count > 0) throw new InvalidOperationException("Initial administrator already exists.");
            var result = PasswordHasher.HashPassword(password);
            _users.Add(new AppUser
            {
                Username = "walid",
                FullName = "Walid Dahou",
                PasswordHash = result.Hash,
                PasswordSalt = result.Salt,
                Role = "Administrator",
                IsActive = true,
                IsProtected = true
            });
            Save();
            Audit("walid", "Administrator created", "Protected initial administrator account");
        }
    }

    public void AddUser(string username, string fullName, string password)
    {
        lock (_sync)
        {
            username = username.Trim();
            if (string.IsNullOrWhiteSpace(username)) throw new InvalidOperationException("Username is required.");
            if (_users.Any(x => string.Equals(x.Username, username, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("That username already exists.");
            var result = PasswordHasher.HashPassword(password);
            _users.Add(new AppUser
            {
                Username = username,
                FullName = fullName.Trim(),
                PasswordHash = result.Hash,
                PasswordSalt = result.Salt,
                Role = "User",
                IsActive = true
            });
            Save();
            Audit(CurrentSession.Username, "User created", username);
        }
    }

    public void ResetPassword(Guid id, string password)
    {
        lock (_sync)
        {
            var user = Find(id);
            var result = PasswordHasher.HashPassword(password);
            user.PasswordHash = result.Hash;
            user.PasswordSalt = result.Salt;
            Save();
            Audit(CurrentSession.Username, "Password reset", user.Username);
        }
    }

    public void UpdateUser(Guid id, string fullName, bool active)
    {
        lock (_sync)
        {
            var user = Find(id);
            user.FullName = fullName.Trim();
            if (!user.IsProtected) user.IsActive = active;
            Save();
            Audit(CurrentSession.Username, "User updated", user.Username);
        }
    }

    public void DeleteUser(Guid id)
    {
        lock (_sync)
        {
            var user = Find(id);
            if (user.IsProtected) throw new InvalidOperationException("The protected Walid administrator account cannot be deleted.");
            _users.Remove(user);
            Save();
            Audit(CurrentSession.Username, "User deleted", user.Username);
        }
    }

    public void ChangeOwnPassword(Guid id, string currentPassword, string newPassword)
    {
        lock (_sync)
        {
            var user = Find(id);
            if (!PasswordHasher.Verify(currentPassword, user.PasswordHash, user.PasswordSalt))
                throw new InvalidOperationException("Current password is incorrect.");
            var result = PasswordHasher.HashPassword(newPassword);
            user.PasswordHash = result.Hash;
            user.PasswordSalt = result.Salt;
            Save();
            Audit(user.Username, "Password changed", "Own password");
        }
    }

    private AppUser Find(Guid id) => _users.FirstOrDefault(x => x.Id == id) ?? throw new InvalidOperationException("User not found.");

    private void Load()
    {
        try
        {
            if (File.Exists(_usersFile))
                _users = JsonSerializer.Deserialize<List<AppUser>>(File.ReadAllText(_usersFile), _json) ?? new();
        }
        catch
        {
            _users = new();
        }
    }

    private void Save()
    {
        var temp = _usersFile + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(_users, _json));
        File.Move(temp, _usersFile, true);
    }

    public void Audit(string? username, string action, string details)
    {
        try
        {
            var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}\t{username ?? "system"}\t{action}\t{details}{Environment.NewLine}";
            File.AppendAllText(_auditFile, line);
        }
        catch { }
    }

    private static AppUser Clone(AppUser x) => new()
    {
        Id = x.Id, Username = x.Username, FullName = x.FullName, PasswordHash = x.PasswordHash,
        PasswordSalt = x.PasswordSalt, Role = x.Role, IsActive = x.IsActive, IsProtected = x.IsProtected,
        CreatedUtc = x.CreatedUtc, LastLoginUtc = x.LastLoginUtc
    };
}
