using System.ComponentModel.DataAnnotations;

namespace proyectoHabitar_server.Modelos
{
    public static class TiposMovimiento
    {
        public const string Ingreso = "Ingreso";
        public const string Egreso = "Egreso";
    }

    public class MovimientoDto
    {
        public int Id { get; set; }
        public DateTime FechaHora { get; set; }
        public string Tipo { get; set; } = "";
        public string Categoria { get; set; } = "";
        public string Concepto { get; set; } = "";
        public string Detalle { get; set; } = "";
        public decimal Monto { get; set; }
        public string MedioPago { get; set; } = "";
        public string Comprobante { get; set; } = "";
        public string UsuarioNombre { get; set; } = "";
        public DateTime? FechaRegistro { get; set; }
        public bool Anulado { get; set; }
        public string MotivoAnulacion { get; set; } = "";
        public DateTime? FechaAnulacion { get; set; }
        public string UsuarioAnulacion { get; set; } = "";
    }

    public class MovimientoGuardarDto
    {
        [Required(ErrorMessage = "Indicá fecha y hora del movimiento.")]
        public DateTime? FechaHora { get; set; }

        [Required(ErrorMessage = "Indicá si es ingreso o egreso."), RegularExpression("^(Ingreso|Egreso)$", ErrorMessage = "El tipo debe ser Ingreso o Egreso.")]
        public string Tipo { get; set; } = "";

        [Required(ErrorMessage = "Indicá la categoría."), StringLength(60)]
        public string Categoria { get; set; } = "";

        [Required(ErrorMessage = "Indicá el concepto."), StringLength(150)]
        public string Concepto { get; set; } = "";

        [StringLength(500)]
        public string? Detalle { get; set; }

        // Límites numéricos (no como texto): con cultura es-AR "0.01" no se interpretaría como decimal.
        [Range(0.01, 999999999999.0, ErrorMessage = "El monto debe ser mayor a cero.")]
        public decimal Monto { get; set; }

        [Required(ErrorMessage = "Indicá el medio de pago."), StringLength(40)]
        public string MedioPago { get; set; } = "";

        [StringLength(60)]
        public string? Comprobante { get; set; }
    }

    public class AnularMovimientoDto
    {
        [Required(ErrorMessage = "Indicá el motivo de la anulación."), StringLength(200, MinimumLength = 5, ErrorMessage = "El motivo debe tener entre 5 y 200 caracteres.")]
        public string Motivo { get; set; } = "";
    }

    public class TotalCategoriaDto
    {
        public string Tipo { get; set; } = "";
        public string Categoria { get; set; } = "";
        public decimal Total { get; set; }
        public int Cantidad { get; set; }
    }

    public class ResumenFondosDto
    {
        public decimal SaldoAnterior { get; set; }
        public decimal TotalIngresos { get; set; }
        public decimal TotalEgresos { get; set; }
        public decimal SaldoPeriodo { get; set; }
        public decimal SaldoFinal { get; set; }
        public decimal SaldoActual { get; set; }
        public int CantidadMovimientos { get; set; }
        public List<TotalCategoriaDto> PorCategoria { get; set; } = new();
    }
}
