using System.ComponentModel.DataAnnotations;

namespace marks_api.Dtos.Courses;

public class UpdateCourseRequest
{
    [Required]
    [StringLength(150)]
    public string Title { get; set; } = string.Empty;

    [Range(1, 4)]
    public int CreditHours { get; set; }
}
