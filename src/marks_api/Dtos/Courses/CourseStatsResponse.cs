namespace marks_api.Dtos.Courses;

public class CourseStatsResponse
{
    public int CourseId { get; set; }

    public string CourseCode { get; set; } = string.Empty;

    public string CourseTitle { get; set; } = string.Empty;

    public int TotalEnrolled { get; set; }

    public int GradedCount { get; set; }

    public int UngradedCount { get; set; }

    public double? AverageMark { get; set; }

    public int? HighestMark { get; set; }

    public int? LowestMark { get; set; }
}
