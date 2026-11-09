namespace marks_api.Data.Entities;

public class Student
{
    public int Id { get; set; }

    public string RegistrationNo { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }

    // Navigation properties
    public ICollection<Enrollment> Enrollments { get; set; }
        = new List<Enrollment>();

    public ICollection<User> Users { get; set; }
        = new List<User>();
}