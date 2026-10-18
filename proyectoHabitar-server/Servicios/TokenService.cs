using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace proyectoHabitar_server.Servicios
{
    public class TokenService
    {
        public const string ClaimNombreCompleto = "nombre";

        private readonly IConfiguration _config;

        public TokenService(IConfiguration config)
        {
            _config = config;
        }

        public (string Token, DateTime Expira) Crear(int id, string usuario, string nombreCompleto, string rol)
        {
            var clave = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
            var expira = DateTime.UtcNow.AddHours(_config.GetValue<int?>("Jwt:ExpiraHoras") ?? 10);

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, id.ToString()),
                new Claim(ClaimTypes.Name, usuario),
                new Claim(ClaimTypes.Role, rol),
                new Claim(ClaimNombreCompleto, nombreCompleto)
            };

            var token = new JwtSecurityToken(
                issuer: _config["Jwt:Issuer"],
                audience: _config["Jwt:Audience"],
                claims: claims,
                expires: expira,
                signingCredentials: new SigningCredentials(clave, SecurityAlgorithms.HmacSha256));

            return (new JwtSecurityTokenHandler().WriteToken(token), expira);
        }
    }

    public static class UsuarioActualExtensiones
    {
        public static int IdUsuario(this ClaimsPrincipal user) =>
            int.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out int id) ? id : 0;

        public static string NombreParaAuditoria(this ClaimsPrincipal user)
        {
            string nombre = user.FindFirstValue(TokenService.ClaimNombreCompleto) ?? "";
            string usuario = user.Identity?.Name ?? "";
            return string.IsNullOrWhiteSpace(nombre) ? usuario : $"{nombre} ({usuario})";
        }
    }
}
