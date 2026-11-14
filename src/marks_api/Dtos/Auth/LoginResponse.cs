namespace marks_api.Dtos.Auth;

public class LoginResponse
{
    public string Token { get; set; } = string.Empty;

    public string Username { get; set; } = string.Empty;

    public string Role { get; set; } = string.Empty;

    public DateTime ExpiresAtUtc { get; set; }
}
