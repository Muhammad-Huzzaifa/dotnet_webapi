using System.Security.Claims;

namespace marks_api.Extensions;

public static class ClaimsPrincipalExtensions
{
    public static bool IsTeacher(this ClaimsPrincipal principal)
        => principal.IsInRole("Teacher");

    public static int? GetStudentId(this ClaimsPrincipal principal)
    {
        var value = principal.FindFirst("studentId")?.Value;

        return int.TryParse(value, out var studentId)
            ? studentId
            : null;
    }

    public static bool CanAccessStudent(
        this ClaimsPrincipal principal,
        int studentId)
        => principal.IsTeacher()
            || principal.GetStudentId() == studentId;
}
