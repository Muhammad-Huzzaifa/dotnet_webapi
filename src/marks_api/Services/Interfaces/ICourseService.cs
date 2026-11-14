using marks_api.Common;
using marks_api.Dtos.Courses;

namespace marks_api.Services.Interfaces;

public interface ICourseService
{
    Task<ServiceResult<IReadOnlyList<CourseResponse>>> GetAllAsync(
        CancellationToken cancellationToken = default);

    Task<ServiceResult<CourseResponse>> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<CourseStatsResponse>> GetStatsAsync(
        int id,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<CourseResponse>> CreateAsync(
        CreateCourseRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<CourseResponse>> UpdateAsync(
        int id,
        UpdateCourseRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult> DeleteAsync(
        int id,
        CancellationToken cancellationToken = default);
}
