using System.ComponentModel.DataAnnotations;

namespace proyectoHabitar_server.Modelos
{
    public static class Roles
    {
        public const string Administrador = "Administrador";
        public const string Encuestador = "Encuestador";
        public const string Logistica = "Logística";

        public const string PatronValidos = "^(Administrador|Encuestador|Logística)$";
    }

    public class UsuarioDto
    {
        public int Id { get; set; }
        public string Usuario { get; set; } = "";
        public string Email { get; set; } = "";
        public string Nombre { get; set; } = "";
        public string Apellido { get; set; } = "";
        public string DNI { get; set; } = "";
        public string Rol { get; set; } = "";
        public bool Activo { get; set; }
    }

    public class UsuarioCrearDto
    {
        [Required(ErrorMessage = "El nombre de usuario es obligatorio."), StringLength(50)]
        public string Usuario { get; set; } = "";

        [Required(ErrorMessage = "La contraseña es obligatoria."), MinLength(6, ErrorMessage = "La contraseña debe tener al menos 6 caracteres.")]
        public string Contrasena { get; set; } = "";

        [Required(ErrorMessage = "El nombre es obligatorio.")]
        public string Nombre { get; set; } = "";

        [Required(ErrorMessage = "El apellido es obligatorio.")]
        public string Apellido { get; set; } = "";

        public string? DNI { get; set; }
        public string? Email { get; set; }

        [Required(ErrorMessage = "El rol es obligatorio."), RegularExpression(Roles.PatronValidos, ErrorMessage = "Rol inválido.")]
        public string Rol { get; set; } = "";
    }

    public class UsuarioEditarDto
    {
        [Required(ErrorMessage = "El nombre es obligatorio.")]
        public string Nombre { get; set; } = "";

        [Required(ErrorMessage = "El apellido es obligatorio.")]
        public string Apellido { get; set; } = "";

        public string? DNI { get; set; }
        public string? Email { get; set; }

        [Required(ErrorMessage = "El rol es obligatorio."), RegularExpression(Roles.PatronValidos, ErrorMessage = "Rol inválido.")]
        public string Rol { get; set; } = "";

        public bool Activo { get; set; } = true;

        /// <summary>Opcional: si viene con texto, se blanquea la contraseña del usuario.</summary>
        public string? Contrasena { get; set; }
    }
}
