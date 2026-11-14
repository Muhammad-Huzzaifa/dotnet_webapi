using System.ComponentModel.DataAnnotations;

namespace marks_api.Dtos.Students;

public class UpdateStudentRequest
{
    [Required]
    [StringLength(100)]
    public string FullName { get; set; } = string.Empty;

    [Required]
    [StringLength(150)]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;
}
