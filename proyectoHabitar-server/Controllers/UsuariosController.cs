using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Text.Json;

namespace proyectoHabitar_server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UsuariosController : ControllerBase
    {
        private readonly string _connectionString = "Server=(localdb)\\MSSQLLocalDB;Database=habitarBD;Trusted_Connection=True;";

        [HttpGet]
        public IActionResult GetUsuarios()
        {
            var lista = new List<object>();

            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                string query = "SELECT Id, Usuario, Email, Nombre, Apellido, DNI, Rol, Activo FROM Usuarios";
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    try
                    {
                        conn.Open();
                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                lista.Add(new
                                {
                                    Id = reader.GetInt32(0),
                                    Usuario = reader.GetString(1),
                                    Email = reader.IsDBNull(2) ? "" : reader.GetString(2),
                                    Nombre = reader.IsDBNull(3) ? "" : reader.GetString(3),
                                    Apellido = reader.IsDBNull(4) ? "" : reader.GetString(4),
                                    DNI = reader.IsDBNull(5) ? "" : reader.GetString(5),
                                    Rol = reader.IsDBNull(6) ? "" : reader.GetString(6),
                                    Activo = reader.GetBoolean(7)
                                });
                            }
                        }
                        return Ok(lista);
                    }
                    catch (Exception ex)
                    {
                        return StatusCode(500, $"Error interno: {ex.Message}");
                    }
                }
            }
        }


        [HttpGet("{id}")]
        public IActionResult GetUsuarioById(int id)
        {
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                string query = "SELECT Id, Usuario, Email, Nombre, Apellido, DNI, Rol, Activo FROM Usuarios WHERE Id = @Id";
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@Id", id);
                    try
                    {
                        conn.Open();
                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                return Ok(new
                                {
                                    Id = reader.GetInt32(0),
                                    Usuario = reader.GetString(1),
                                    Email = reader.IsDBNull(2) ? "" : reader.GetString(2),
                                    Nombre = reader.IsDBNull(3) ? "" : reader.GetString(3),
                                    Apellido = reader.IsDBNull(4) ? "" : reader.GetString(4),
                                    DNI = reader.IsDBNull(5) ? "" : reader.GetString(5),
                                    Rol = reader.IsDBNull(6) ? "" : reader.GetString(6),
                                    Activo = reader.GetBoolean(7)
                                });
                            }
                            return NotFound($"Usuario con Id {id} no encontrado.");
                        }
                    }
                    catch (Exception ex)
                    {
                        return StatusCode(500, $"Error interno: {ex.Message}");
                    }
                }
            }
        }

        [HttpPost]
        public IActionResult CrearUsuario([FromBody] dynamic nuevoUsuario)
        {
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                string query = @"INSERT INTO Usuarios (Usuario, Contrasena, Email, Nombre, Apellido, DNI, Rol, Activo) 
                                 VALUES (@Usuario, @Contrasena, @Email, @Nombre, @Apellido, @DNI, @Rol, 1)";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    try
                    {
                      
                        var elemento = JsonDocument.Parse(nuevoUsuario.ToString()).RootElement;

                        cmd.Parameters.AddWithValue("@Usuario", elemento.GetProperty("Usuario").GetString());
                        cmd.Parameters.AddWithValue("@Contrasena", elemento.GetProperty("Contrasena").GetString());
                        cmd.Parameters.AddWithValue("@Email", elemento.GetProperty("Email").GetString());
                        cmd.Parameters.AddWithValue("@Nombre", elemento.GetProperty("Nombre").GetString());
                        cmd.Parameters.AddWithValue("@Apellido", elemento.GetProperty("Apellido").GetString());
                        cmd.Parameters.AddWithValue("@DNI", elemento.GetProperty("DNI").GetString());
                        cmd.Parameters.AddWithValue("@Rol", elemento.GetProperty("Rol").GetString());

                        conn.Open();
                        cmd.ExecuteNonQuery();
                        return Ok(new { mensaje = "Usuario creado con éxito." });
                    }
                    catch (Exception ex)
                    {
                        return StatusCode(500, $"Error al guardar: {ex.Message}");
                    }
                }
            }
        }

        [HttpPut("{id}")]
        public IActionResult ModificarUsuario(int id, [FromBody] dynamic datosActualizados)
        {
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                string query = @"UPDATE Usuarios 
                                 SET Email = @Email, Nombre = @Nombre, Apellido = @Apellido, DNI = @DNI, Rol = @Rol, Activo = @Activo 
                                 WHERE Id = @Id";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    try
                    {
                        var elemento = JsonDocument.Parse(datosActualizados.ToString()).RootElement;

                        cmd.Parameters.AddWithValue("@Id", id);
                        cmd.Parameters.AddWithValue("@Email", elemento.GetProperty("Email").GetString());
                        cmd.Parameters.AddWithValue("@Nombre", elemento.GetProperty("Nombre").GetString());
                        cmd.Parameters.AddWithValue("@Apellido", elemento.GetProperty("Apellido").GetString());
                        cmd.Parameters.AddWithValue("@DNI", elemento.GetProperty("DNI").GetString());
                        cmd.Parameters.AddWithValue("@Rol", elemento.GetProperty("Rol").GetString());
                        cmd.Parameters.AddWithValue("@Activo", elemento.GetProperty("Activo").GetBoolean());

                        conn.Open();
                        int filasAfectadas = cmd.ExecuteNonQuery();

                        if (filasAfectadas > 0)
                            return Ok(new { mensaje = "Usuario actualizado correctamente." });

                        return NotFound("No se encontró el usuario para modificar.");
                    }
                    catch (Exception ex)
                    {
                        return StatusCode(500, $"Error al actualizar: {ex.Message}");
                    }
                }
            }
        }


        [HttpDelete("{id}")]
        public IActionResult EliminarUsuario(int id)
        {
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
               
                string query = "UPDATE Usuarios SET Activo = 0 WHERE Id = @Id";
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@Id", id);
                    try
                    {
                        conn.Open();
                        int filasAfectadas = cmd.ExecuteNonQuery();

                        if (filasAfectadas > 0)
                            return Ok(new { mensaje = "Usuario dado de baja (Inactivo) correctamente." });

                        return NotFound("No se encontró el usuario.");
                    }
                    catch (Exception ex)
                    {
                        return StatusCode(500, $"Error al dar de baja: {ex.Message}");
                    }
                }
            }
        }
    }
}