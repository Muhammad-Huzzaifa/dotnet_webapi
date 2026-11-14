namespace marks_api.SmokeTests;

public sealed class SmokeTestData
{
    public string TeacherToken { get; set; } = string.Empty;

    public string StudentToken { get; set; } = string.Empty;

    public int StudentId { get; set; }

    public int? OtherStudentId { get; set; }

    public string StudentRegistrationNo { get; set; } = string.Empty;

    public int SeededCourseId { get; set; }

    public string CourseCode { get; set; } = string.Empty;

    public string CourseTitle { get; set; } = string.Empty;

    public int? CreatedStudentId { get; set; }

    public string CreatedStudentRegistrationNo { get; set; } = string.Empty;

    public int? CreatedCourseId { get; set; }

    public int? CreatedEnrollmentId { get; set; }
}
