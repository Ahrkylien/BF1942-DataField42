namespace DataField42.Website.Data.Entities;

/// <summary>
/// An account that may reach the admin pages. Passwords are stored only as a hash, produced by
/// ASP.NET Core's own password hasher.
/// </summary>
public class AdminUser
{
    public int Id { get; set; }

    public string UserName { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public DateTime CreatedUtc { get; set; }

    public DateTime? LastLoginUtc { get; set; }

    /// <summary>
    /// Incremented whenever the password changes, so sessions issued against the old password
    /// stop validating.
    /// </summary>
    public int SecurityStamp { get; set; }
}
