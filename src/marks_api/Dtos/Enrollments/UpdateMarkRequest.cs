using System.ComponentModel.DataAnnotations;

namespace marks_api.Dtos.Enrollments;

public class UpdateMarkRequest
{
    [Range(0, 100)]
    public int? Mark { get; set; }
}
