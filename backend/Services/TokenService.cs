using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Backend.Config;
using Backend.Models;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace backend.Services;

public class TokenService : ITokenService
{
    private readonly JwtOptions _options;
    private readonly SymmetricSecurityKey _key;

    public TokenService(IOptions<JwtOptions> options)
    {
        _options = options.Value;
        _key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Key));
    }

    public string CreateToken(User user)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.NameId, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email!),
            new(JwtRegisteredClaimNames.GivenName, user.FirstName),
            new(JwtRegisteredClaimNames.FamilyName, user.LastName)
        };

        var credentials = new SigningCredentials(_key, SecurityAlgorithms.HmacSha512Signature);
        var tokenExpiry = DateTime.UtcNow.AddMinutes(_options.DurationInMinutes);

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = tokenExpiry,
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            SigningCredentials = credentials
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var token = tokenHandler.CreateToken(tokenDescriptor);

        return tokenHandler.WriteToken(token);
    }
}