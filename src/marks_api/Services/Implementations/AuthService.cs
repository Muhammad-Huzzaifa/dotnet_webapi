using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using marks_api.Data;
using marks_api.Data.Entities;
using marks_api.Dtos.Auth;
using marks_api.Services.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace marks_api.Services.Implementations;

public class AuthService : IAuthService
{
    private const string SecretKeyConfig = "JwtSettings:SecretKey";
    private const string IssuerConfig = "JwtSettings:Issuer";
    private const string AudienceConfig = "JwtSettings:Audience";
    private const string ExpirationConfig = "JwtSettings:ExpirationMinutes";
    private const int DefaultExpirationMinutes = 60;

    private readonly AppDbContext _context;
    private readonly PasswordHasher<User> _passwordHasher;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        AppDbContext context,
        PasswordHasher<User> passwordHasher,
        IConfiguration configuration,
        ILogger<AuthService> logger)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<LoginResponse?> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default)
    {
        var user = await _context.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(
                u => u.Username == request.Username,
                cancellationToken);

        if (user is null)
        {
            // Do not reveal whether the username or the password was wrong.
            _logger.LogWarning(
                "Login failed for unknown username {Username}.",
                request.Username);

            return null;
        }

        var verificationResult = _passwordHasher.VerifyHashedPassword(
            user,
            user.PasswordHash,
            request.Password);

        if (verificationResult == PasswordVerificationResult.Failed)
        {
            _logger.LogWarning(
                "Login failed for user {Username}: invalid password.",
                user.Username);

            return null;
        }

        if (verificationResult == PasswordVerificationResult.SuccessRehashNeeded)
        {
            await UpgradePasswordHashAsync(
                user.Id,
                request.Password,
                cancellationToken);
        }

        var (token, expiresAtUtc) = GenerateToken(user);

        _logger.LogInformation(
            "Login succeeded for user {Username} with role {Role}.",
            user.Username,
            user.Role);

        return new LoginResponse
        {
            Token = token,
            Username = user.Username,
            Role = user.Role,
            ExpiresAtUtc = expiresAtUtc
        };
    }

    private async Task UpgradePasswordHashAsync(
        int userId,
        string password,
        CancellationToken cancellationToken)
    {
        var trackedUser = await _context.Users
            .SingleAsync(u => u.Id == userId, cancellationToken);

        trackedUser.PasswordHash = _passwordHasher.HashPassword(
            trackedUser,
            password);

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Password hash upgraded for user {Username}.",
            trackedUser.Username);
    }

    private (string Token, DateTime ExpiresAtUtc) GenerateToken(User user)
    {
        var secretKey = _configuration[SecretKeyConfig]
            ?? throw new InvalidOperationException(
                $"{SecretKeyConfig} is not configured.");

        var issuer = _configuration[IssuerConfig]
            ?? throw new InvalidOperationException(
                $"{IssuerConfig} is not configured.");

        var audience = _configuration[AudienceConfig]
            ?? throw new InvalidOperationException(
                $"{AudienceConfig} is not configured.");

        var expirationMinutes = int.TryParse(
            _configuration[ExpirationConfig],
            out var configuredMinutes)
            ? configuredMinutes
            : DefaultExpirationMinutes;

        var signingKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(secretKey));

        var signingCredentials = new SigningCredentials(
            signingKey,
            SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.UniqueName, user.Username),
            new("role", user.Role),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        if (user.StudentId is int studentId)
        {
            claims.Add(new Claim("studentId", studentId.ToString()));
        }

        var now = DateTime.UtcNow;
        var expiresAtUtc = now.AddMinutes(expirationMinutes);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            notBefore: now,
            expires: expiresAtUtc,
            signingCredentials: signingCredentials);

        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAtUtc);
    }
}
