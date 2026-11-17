using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using EventHop.Api.Models;
using Microsoft.IdentityModel.Tokens;

namespace EventHop.Api.Services;

public class TokenService
{
    private readonly IConfiguration configuracion;

    public TokenService(IConfiguration configuracion)
    {
        this.configuracion = configuracion;
    }

    public string Crear(Usuario usuario, DateTime vencimiento)
    {
        var datos = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, usuario.Id.ToString()),
            new Claim(ClaimTypes.Role, usuario.TipoUsuario),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var llave = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(configuracion["Jwt:Key"]!));
        var firma = new SigningCredentials(llave, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: configuracion["Jwt:Issuer"],
            audience: configuracion["Jwt:Audience"],
            claims: datos,
            expires: vencimiento,
            signingCredentials: firma);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
