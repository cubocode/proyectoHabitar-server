using System.Data.SqlClient;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using proyectoHabitar_server.Datos;
using proyectoHabitar_server.Modelos;
using proyectoHabitar_server.Servicios;

namespace proyectoHabitar_server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = Roles.Administrador)]
    public class UsuariosController : ControllerBase
    {
        private const string Columnas = "Id, Usuario, Email, Nombre, Apellido, DNI, Rol, Activo";

        private readonly Db _db;

        public UsuariosController(Db db)
        {
            _db = db;
        }

        [HttpGet]
        public ActionResult<List<UsuarioDto>> GetUsuarios()
        {
            var lista = new List<UsuarioDto>();

            using var conexion = _db.Abrir();
            using var comando = new SqlCommand($"SELECT {Columnas} FROM Usuarios ORDER BY Activo DESC, Apellido, Nombre", conexion);
            using var reader = comando.ExecuteReader();
            while (reader.Read())
                lista.Add(Leer(reader));

            return Ok(lista);
        }

        [HttpGet("{id:int}")]
        public ActionResult<UsuarioDto> GetUsuarioById(int id)
        {
            using var conexion = _db.Abrir();
            using var comando = new SqlCommand($"SELECT {Columnas} FROM Usuarios WHERE Id = @Id", conexion);
            comando.Parameters.AddWithValue("@Id", id);
            using var reader = comando.ExecuteReader();

            if (reader.Read())
                return Ok(Leer(reader));

            return NotFound(new { mensaje = $"Usuario con Id {id} no encontrado." });
        }

        [HttpPost]
        public IActionResult CrearUsuario([FromBody] UsuarioCrearDto nuevo)
        {
            const string query = @"INSERT INTO Usuarios (Usuario, Contrasena, Email, Nombre, Apellido, DNI, Rol, Activo)
                                   VALUES (@Usuario, @Contrasena, @Email, @Nombre, @Apellido, @DNI, @Rol, 1);
                                   SELECT CAST(SCOPE_IDENTITY() AS INT);";
            try
            {
                using var conexion = _db.Abrir();

                if (ExisteUsuario(conexion, nuevo.Usuario.Trim()))
                    return Conflict(new { mensaje = $"Ya existe el usuario '{nuevo.Usuario.Trim()}'." });

                using var comando = new SqlCommand(query, conexion);
                comando.Parameters.AddWithValue("@Usuario", nuevo.Usuario.Trim());
                comando.Parameters.AddWithValue("@Contrasena", BCrypt.Net.BCrypt.HashPassword(nuevo.Contrasena));
                comando.Parameters.AddWithValue("@Email", nuevo.Email?.Trim() ?? "");
                comando.Parameters.AddWithValue("@Nombre", nuevo.Nombre.Trim());
                comando.Parameters.AddWithValue("@Apellido", nuevo.Apellido.Trim());
                comando.Parameters.AddWithValue("@DNI", nuevo.DNI?.Trim() ?? "");
                comando.Parameters.AddWithValue("@Rol", nuevo.Rol);

                int id = (int)comando.ExecuteScalar();
                return Ok(new { id, mensaje = "Usuario creado con éxito." });
            }
            catch (SqlException ex) when (ex.EsClaveDuplicada())
            {
                return Conflict(new { mensaje = $"Ya existe el usuario '{nuevo.Usuario.Trim()}'." });
            }
        }

        [HttpPut("{id:int}")]
        public IActionResult ModificarUsuario(int id, [FromBody] UsuarioEditarDto datos)
        {
            if (id == User.IdUsuario() && (!datos.Activo || datos.Rol != Roles.Administrador))
                return Conflict(new { mensaje = "No podés quitarte el rol de Administrador ni desactivar tu propio usuario." });

            bool cambiaContrasena = !string.IsNullOrWhiteSpace(datos.Contrasena);
            if (cambiaContrasena && datos.Contrasena!.Length < 6)
                return BadRequest(new { mensaje = "La nueva contraseña debe tener al menos 6 caracteres." });

            string query = @"UPDATE Usuarios
                             SET Email = @Email, Nombre = @Nombre, Apellido = @Apellido, DNI = @DNI, Rol = @Rol, Activo = @Activo"
                           + (cambiaContrasena ? ", Contrasena = @Contrasena" : "")
                           + " WHERE Id = @Id";

            using var conexion = _db.Abrir();
            using var comando = new SqlCommand(query, conexion);
            comando.Parameters.AddWithValue("@Id", id);
            comando.Parameters.AddWithValue("@Email", datos.Email?.Trim() ?? "");
            comando.Parameters.AddWithValue("@Nombre", datos.Nombre.Trim());
            comando.Parameters.AddWithValue("@Apellido", datos.Apellido.Trim());
            comando.Parameters.AddWithValue("@DNI", datos.DNI?.Trim() ?? "");
            comando.Parameters.AddWithValue("@Rol", datos.Rol);
            comando.Parameters.AddWithValue("@Activo", datos.Activo);
            if (cambiaContrasena)
                comando.Parameters.AddWithValue("@Contrasena", BCrypt.Net.BCrypt.HashPassword(datos.Contrasena));

            if (comando.ExecuteNonQuery() > 0)
                return Ok(new { mensaje = "Usuario actualizado correctamente." });

            return NotFound(new { mensaje = "No se encontró el usuario para modificar." });
        }

        /// <summary>Baja lógica: el usuario queda inactivo y ya no puede iniciar sesión.</summary>
        [HttpDelete("{id:int}")]
        public IActionResult EliminarUsuario(int id)
        {
            if (id == User.IdUsuario())
                return Conflict(new { mensaje = "No podés dar de baja tu propio usuario." });

            using var conexion = _db.Abrir();
            using var comando = new SqlCommand("UPDATE Usuarios SET Activo = 0 WHERE Id = @Id", conexion);
            comando.Parameters.AddWithValue("@Id", id);

            if (comando.ExecuteNonQuery() > 0)
                return Ok(new { mensaje = "Usuario dado de baja (inactivo) correctamente." });

            return NotFound(new { mensaje = "No se encontró el usuario." });
        }

        private static bool ExisteUsuario(SqlConnection conexion, string usuario)
        {
            using var comando = new SqlCommand("SELECT COUNT(1) FROM Usuarios WHERE Usuario = @Usuario", conexion);
            comando.Parameters.AddWithValue("@Usuario", usuario);
            return Convert.ToInt32(comando.ExecuteScalar()) > 0;
        }

        private static UsuarioDto Leer(SqlDataReader r) => new()
        {
            Id = r.Entero("Id"),
            Usuario = r.Texto("Usuario"),
            Email = r.Texto("Email"),
            Nombre = r.Texto("Nombre"),
            Apellido = r.Texto("Apellido"),
            DNI = r.Texto("DNI"),
            Rol = r.Texto("Rol"),
            Activo = r.Booleano("Activo")
        };
    }
}
