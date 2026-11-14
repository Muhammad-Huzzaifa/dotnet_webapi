namespace marks_api.Dtos.Enrollments;

public class EnrollmentResponse
{
    public int Id { get; set; }

    public int StudentId { get; set; }

    public string StudentName { get; set; } = string.Empty;

    public string RegistrationNo { get; set; } = string.Empty;

    public int CourseId { get; set; }

    public string CourseCode { get; set; } = string.Empty;

    public string CourseTitle { get; set; } = string.Empty;

    public int? Mark { get; set; }

    public DateTime EnrolledAtUtc { get; set; }
}
