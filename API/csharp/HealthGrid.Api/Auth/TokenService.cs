using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using HealthGrid.Api.Data;
using HealthGrid.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace HealthGrid.Api.Auth;

public sealed record IssuedTokens(string AccessToken, string RefreshToken, DateTime ExpiresAtUtc);

/// <summary>
/// Issues short-lived JWT access tokens and rotating refresh tokens. Refresh
/// tokens are stored only as SHA-256 hashes; the raw value is returned once.
/// </summary>
public sealed class TokenService(
    HealthGridDbContext db,
    IOptions<JwtOptions> options)
{
    private readonly JwtOptions _opt = options.Value;

    public async Task<IssuedTokens> IssueAsync(
        ApplicationUser user, IEnumerable<string> roles, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var expires = now.AddMinutes(_opt.AccessTokenMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new(ClaimTypes.Name, user.FullName),
            new(ClaimNames.TokenVersion, user.TokenVersion.ToString()),
        };
        if (user.DistrictId is { } d)
            claims.Add(new Claim(ClaimNames.DistrictId, d.ToString()));
        if (user.PhcId is { } p)
            claims.Add(new Claim(ClaimNames.PhcId, p.ToString()));
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_opt.SigningKey));
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _opt.Issuer,
            Audience = _opt.Audience,
            Subject = new ClaimsIdentity(claims),
            IssuedAt = now,
            NotBefore = now,
            Expires = expires,
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256),
        };

        var accessToken = new JsonWebTokenHandler().CreateToken(descriptor);

        var raw = RandomNumberGenerator.GetHexString(64);
        db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = Hash(raw),
            ExpiresAtUtc = now.AddDays(_opt.RefreshTokenDays),
        });
        await db.SaveChangesAsync(ct);

        return new IssuedTokens(accessToken, raw, expires);
    }

    /// <summary>Validates a refresh token, revokes it, and returns the owning user id.</summary>
    public async Task<Guid?> ConsumeRefreshTokenAsync(string raw, CancellationToken ct)
    {
        var hash = Hash(raw);
        var token = await db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, ct);
        if (token is null || !token.IsActive)
            return null;

        token.RevokedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return token.UserId;
    }

    public async Task RevokeRefreshTokenAsync(string raw, CancellationToken ct)
    {
        var hash = Hash(raw);
        var token = await db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, ct);
        if (token is { RevokedAtUtc: null })
        {
            token.RevokedAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
        }
    }

    private static string Hash(string value)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
