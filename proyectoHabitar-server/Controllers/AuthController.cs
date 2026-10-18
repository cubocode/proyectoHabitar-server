using System.Data.SqlClient;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using proyectoHabitar_server.Datos;
using proyectoHabitar_server.Modelos;
using proyectoHabitar_server.Servicios;

namespace proyectoHabitar_server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly Db _db;
        private readonly TokenService _tokens;

        public AuthController(Db db, TokenService tokens)
        {
            _db = db;
            _tokens = tokens;
        }

        [AllowAnonymous]
        [HttpPost("login")]
        public ActionResult<LoginResponse> Login([FromBody] LoginRequest request)
        {
            using SqlConnection conexion = _db.Abrir();

            const string query = "SELECT Id, Usuario, Contrasena, Nombre, Apellido, Rol, Activo FROM Usuarios WHERE Usuario = @Usuario";
            using var comando = new SqlCommand(query, conexion);
            comando.Parameters.AddWithValue("@Usuario", request.Usuario.Trim());

            int id;
            string usuario, guardada, nombre, apellido, rol;
            bool activo;
            using (var reader = comando.ExecuteReader())
            {
                if (!reader.Read())
                    return Unauthorized(new { mensaje = "Usuario o contraseña incorrectos." });

                id = reader.Entero("Id");
                usuario = reader.Texto("Usuario");
                guardada = reader.Texto("Contrasena");
                nombre = reader.Texto("Nombre");
                apellido = reader.Texto("Apellido");
                rol = reader.Texto("Rol");
                activo = reader.Booleano("Activo");
            }

            if (!VerificarContrasena(conexion, id, request.Contrasena, guardada))
                return Unauthorized(new { mensaje = "Usuario o contraseña incorrectos." });

            // Se chequea después de la contraseña para no revelar qué usuarios existen.
            if (!activo)
                return StatusCode(StatusCodes.Status403Forbidden, new { mensaje = "Tu usuario está dado de baja. Contactá a un administrador." });

            var (token, expira) = _tokens.Crear(id, usuario, $"{nombre} {apellido}".Trim(), rol);

            return Ok(new LoginResponse
            {
                Token = token,
                Expira = expira,
                Id = id,
                Usuario = usuario,
                Nombre = nombre,
                Apellido = apellido,
                Rol = rol
            });
        }

        /// <summary>
        /// Las contraseñas se guardan con BCrypt. Las que todavía están en texto plano (usuarios
        /// creados antes de este cambio) se aceptan una vez y se convierten a hash en ese mismo login.
        /// </summary>
        private static bool VerificarContrasena(SqlConnection conexion, int idUsuario, string ingresada, string guardada)
        {
            if (EsHashBCrypt(guardada))
                return BCrypt.Net.BCrypt.Verify(ingresada, guardada);

            if (guardada != ingresada)
                return false;

            using var actualizar = new SqlCommand("UPDATE Usuarios SET Contrasena = @Hash WHERE Id = @Id", conexion);
            actualizar.Parameters.AddWithValue("@Hash", BCrypt.Net.BCrypt.HashPassword(ingresada));
            actualizar.Parameters.AddWithValue("@Id", idUsuario);
            actualizar.ExecuteNonQuery();
            return true;
        }

        public static bool EsHashBCrypt(string valor) => valor.StartsWith("$2") && valor.Length == 60;
    }
}
