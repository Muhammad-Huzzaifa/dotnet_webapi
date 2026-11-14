namespace marks_api.Dtos.Students;

public class StudentTranscriptResponse
{
    public int StudentId { get; set; }

    public string RegistrationNo { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public IReadOnlyList<TranscriptCourseResponse> Enrollments { get; set; } = [];

    /// <summary>
    /// Credit-hour weighted average of graded courses.
    /// Null when the student has no graded courses.
    /// </summary>
    public double? WeightedAverage { get; set; }

    public int PassMark { get; set; }

    /// <summary>
    /// Null when there is no weighted average to compare with the pass mark.
    /// </summary>
    public bool? IsPassed { get; set; }
}

public class TranscriptCourseResponse
{
    public string CourseCode { get; set; } = string.Empty;

    public string CourseTitle { get; set; } = string.Empty;

    public int CreditHours { get; set; }

    public int? Mark { get; set; }
}
