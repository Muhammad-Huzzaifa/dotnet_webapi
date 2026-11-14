using System.ComponentModel.DataAnnotations;

namespace marks_api.Dtos.Students;

public class CreateStudentRequest
{
    [Required]
    [StringLength(20)]
    public string RegistrationNo { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string FullName { get; set; } = string.Empty;

    [Required]
    [StringLength(150)]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;
}
