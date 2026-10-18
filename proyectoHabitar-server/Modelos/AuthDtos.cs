using System.ComponentModel.DataAnnotations;

namespace proyectoHabitar_server.Modelos
{
    public class LoginRequest
    {
        [Required(ErrorMessage = "Ingresá el usuario.")]
        public string Usuario { get; set; } = "";

        [Required(ErrorMessage = "Ingresá la contraseña.")]
        public string Contrasena { get; set; } = "";
    }

    public class LoginResponse
    {
        public string Token { get; set; } = "";
        public DateTime Expira { get; set; }
        public int Id { get; set; }
        public string Usuario { get; set; } = "";
        public string Nombre { get; set; } = "";
        public string Apellido { get; set; } = "";
        public string Rol { get; set; } = "";
    }
}
