using System.ComponentModel.DataAnnotations;

namespace marks_api.Dtos.Enrollments;

public class CreateEnrollmentRequest
{
    [Range(1, int.MaxValue)]
    public int StudentId { get; set; }

    [Range(0, 100)]
    public int? Mark { get; set; }
}
