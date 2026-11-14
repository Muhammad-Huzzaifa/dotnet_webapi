using marks_api.Common;
using marks_api.Dtos.Enrollments;

namespace marks_api.Services.Interfaces;

public interface IEnrollmentService
{
    Task<ServiceResult<EnrollmentResponse>> CreateAsync(
        int courseId,
        CreateEnrollmentRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<IReadOnlyList<EnrollmentResponse>>> GetByCourseAsync(
        int courseId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<EnrollmentResponse>> UpdateMarkAsync(
        int id,
        UpdateMarkRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult> DeleteAsync(
        int id,
        CancellationToken cancellationToken = default);
}
