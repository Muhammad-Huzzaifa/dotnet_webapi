using marks_api.Dtos.Auth;

namespace marks_api.Services.Interfaces;

public interface IAuthService
{
    Task<LoginResponse?> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default);
}
