using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using LiftLedger.Api.Domain;
using Microsoft.IdentityModel.Tokens;

namespace LiftLedger.Api.Auth;

public class JwtTokenService
{
    private readonly IConfiguration _configuration;

    public JwtTokenService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public string CreateToken(User user, Tenant tenant, MembershipRole role, TimeSpan? lifetime = null)
    {
        var signingKey = JwtConfiguration.GetSigningKey(_configuration);
        var issuer = _configuration["Jwt:Issuer"] ?? "LiftLedger";
        var audience = _configuration["Jwt:Audience"] ?? "LiftLedger.Clients";

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new("tenant_id", tenant.Id.ToString()),
            new("role", role.ToString()),
            new(ClaimTypes.Role, role.ToString()),
            new("email", user.Email),
            new(ClaimTypes.Email, user.Email),
            new("name", user.FullName),
            new("tenant_name", tenant.Name)
        };

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer,
            audience,
            claims,
            expires: DateTime.UtcNow.Add(lifetime ?? TimeSpan.FromHours(12)),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

public static class JwtConfiguration
{
    public const string DevelopmentFallbackKey = "DEV-ONLY-DO-NOT-USE-IN-PRODUCTION-LiftLedger-32+";

    public static string GetSigningKey(IConfiguration configuration, IHostEnvironment? environment = null)
    {
        var key = configuration["Jwt:SigningKey"];
        if (!string.IsNullOrWhiteSpace(key) && key.Length >= 32)
        {
            return key;
        }

        var envName = environment?.EnvironmentName ?? configuration["ASPNETCORE_ENVIRONMENT"];
        var isDev = string.Equals(envName, "Development", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(envName, "Testing", StringComparison.OrdinalIgnoreCase);

        if (isDev)
        {
            return DevelopmentFallbackKey;
        }

        throw new InvalidOperationException(
            "Jwt:SigningKey must be set to at least 32 characters in non-development environments (Jwt__SigningKey).");
    }
}
