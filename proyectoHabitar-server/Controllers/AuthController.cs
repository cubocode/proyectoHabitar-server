using Microsoft.AspNetCore.Mvc;
using System.Data.SqlClient;
using System;

namespace proyectoHabitar_server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly string _connectionString = @"Server=(localdb)\MSSQLLocalDB;Database=habitarBD;Trusted_Connection=True;TrustServerCertificate=True;";

        [HttpPost("login")]
        public IActionResult Login([FromBody] LoginRequest request)
        {
            try
            {
                
                using (System.Data.SqlClient.SqlConnection connection = new System.Data.SqlClient.SqlConnection(_connectionString))
                {
                    string query = "SELECT COUNT(1) FROM Usuarios WHERE Usuario = @Usuario AND Contrasena = @Contrasena";

                    using (System.Data.SqlClient.SqlCommand command = new System.Data.SqlClient.SqlCommand(query, connection))
                    {
                        
                        command.Parameters.AddWithValue("@Usuario", request.Usuario);
                        command.Parameters.AddWithValue("@Contrasena", request.Contrasena);

                        connection.Open();
                        int count = Convert.ToInt32(command.ExecuteScalar());

                        if (count > 0)
                        {
                            return Ok(new { Exitoso = true, Mensaje = "Login correcto" });
                        }
                    }
                }

                return Unauthorized(new { Exitoso = false, Mensaje = "Usuario o contraseña incorrectos" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Exitoso = false, Mensaje = $"Error interno en el servidor: {ex.Message}" });
            }
        }
    }

    public class LoginRequest
    {
        public string Usuario { get; set; }
        public string Contrasena { get; set; }
    }
}