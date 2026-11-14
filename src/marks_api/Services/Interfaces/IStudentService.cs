using marks_api.Common;
using marks_api.Dtos.Students;

namespace marks_api.Services.Interfaces;

public interface IStudentService
{
    Task<ServiceResult<PagedResult<StudentResponse>>> GetAllAsync(
        StudentQueryParameters query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<StudentResponse>> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<StudentResponse>> CreateAsync(
        CreateStudentRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<StudentResponse>> UpdateAsync(
        int id,
        UpdateStudentRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<StudentTranscriptResponse>> GetTranscriptAsync(
        int id,
        CancellationToken cancellationToken = default);

    Task<ServiceResult> DeleteAsync(
        int id,
        CancellationToken cancellationToken = default);
}
