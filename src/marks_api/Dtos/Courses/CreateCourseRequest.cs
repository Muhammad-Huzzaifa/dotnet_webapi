using System.ComponentModel.DataAnnotations;

namespace marks_api.Dtos.Courses;

public class CreateCourseRequest
{
    [Required]
    [StringLength(10)]
    public string Code { get; set; } = string.Empty;

    [Required]
    [StringLength(150)]
    public string Title { get; set; } = string.Empty;

    [Range(1, 4)]
    public int CreditHours { get; set; }
}
