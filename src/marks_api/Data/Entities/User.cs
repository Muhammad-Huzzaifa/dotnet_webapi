namespace marks_api.Data.Entities;

public class User
{
    public int Id { get; set; }

    public string Username { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public string Role { get; set; } = string.Empty;

    public int? StudentId { get; set; }

    // Navigation property
    public Student? Student { get; set; }
}