using System.ComponentModel.DataAnnotations;

namespace proyectoHabitar_server.Modelos
{
    /// <summary>
    /// Ficha completa del beneficiario. Reemplaza al "dynamic" que recibían POST/PUT:
    /// ahora Swagger muestra el esquema y un dato faltante devuelve 400 con un mensaje claro, no un 500.
    /// </summary>
    public class BeneficiarioDto
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "El CUIL es obligatorio."), RegularExpression(@"^\d{11}$", ErrorMessage = "El CUIL debe tener 11 dígitos, sin guiones.")]
        public string Cuil { get; set; } = "";

        [Required(ErrorMessage = "El DNI es obligatorio."), RegularExpression(@"^\d{7,8}$", ErrorMessage = "El DNI debe tener 7 u 8 dígitos.")]
        public string NumeroDocumento { get; set; } = "";

        [Required(ErrorMessage = "El apellido es obligatorio.")]
        public string Apellido { get; set; } = "";

        [Required(ErrorMessage = "El nombre es obligatorio.")]
        public string Nombre { get; set; } = "";

        [Required(ErrorMessage = "La fecha de nacimiento es obligatoria.")]
        public DateTime? FechaNacimiento { get; set; }

        [Required(ErrorMessage = "Seleccioná la identidad de género.")]
        public string Genero { get; set; } = "";

        [Required(ErrorMessage = "Seleccioná el estado civil.")]
        public string EstadoCivil { get; set; } = "";

        public string? TelefonoCelular { get; set; }
        public string? Email { get; set; }

        [Required(ErrorMessage = "El domicilio es obligatorio.")]
        public string Domicilio { get; set; } = "";

        [Required(ErrorMessage = "La localidad es obligatoria.")]
        public string Localidad { get; set; } = "";

        public string Provincia { get; set; } = "Tucumán";

        [Required(ErrorMessage = "El código postal es obligatorio.")]
        public string CodigoPostal { get; set; } = "";

        [Required(ErrorMessage = "Seleccioná el nivel educativo.")]
        public string NivelEducativo { get; set; } = "";

        [Required(ErrorMessage = "Seleccioná la condición laboral.")]
        public string CondicionLaboral { get; set; } = "";

        [Range(0, 9999999999, ErrorMessage = "Los ingresos no pueden ser negativos.")]
        public decimal IngresosMensuales { get; set; }

        public bool PoseePlanSocial { get; set; }
        public string? OrganismoPlanSocial { get; set; }

        [Range(1, 50, ErrorMessage = "El hogar debe tener entre 1 y 50 integrantes.")]
        public int CantidadConvivientes { get; set; } = 1;

        [Range(0, 30, ErrorMessage = "Cantidad de hijos menores inválida.")]
        public int CantidadHijosMenores { get; set; }

        [Range(0, 30, ErrorMessage = "Cantidad de adultos mayores inválida.")]
        public int CantidadAdultosMayores { get; set; }

        public bool TieneIntegrantesDiscapacidad { get; set; }

        [Required(ErrorMessage = "Seleccioná el tipo de vivienda.")]
        public string TipoViviendaOriginal { get; set; } = "";

        [Required(ErrorMessage = "Seleccioná el régimen de tenencia del terreno.")]
        public string RegimenTenenciaTerreno { get; set; } = "";

        [Required(ErrorMessage = "Seleccioná el material del piso.")]
        public string MaterialPredominantePiso { get; set; } = "";

        [Required(ErrorMessage = "Seleccioná el material del techo.")]
        public string MaterialPredominanteTecho { get; set; } = "";

        [Required(ErrorMessage = "Seleccioná la procedencia del agua.")]
        public string ProcedenciaAgua { get; set; } = "";

        [Required(ErrorMessage = "Seleccioná el tipo de desagüe.")]
        public string DesagueInodoro { get; set; } = "";

        [Range(-90, 90, ErrorMessage = "Latitud inválida.")]
        public double? Latitud { get; set; }

        [Range(-180, 180, ErrorMessage = "Longitud inválida.")]
        public double? Longitud { get; set; }

        public bool UbicacionAproximada { get; set; }

        // Calculados por el servidor (se ignoran si el cliente los manda).
        public DateTime? FechaRelevamiento { get; set; }
        public int PuntajeCriticidad { get; set; }
        public string ColorSemaforo { get; set; } = "";
    }

    public class BeneficiarioListaDto
    {
        public int Id { get; set; }
        public DateTime? FechaRelevamiento { get; set; }
        public string Nombre { get; set; } = "";
        public string Dni { get; set; } = "";
        public string Domicilio { get; set; } = "";
        public string Localidad { get; set; } = "";
        public int PuntajeCriticidad { get; set; }
        public string ColorSemaforo { get; set; } = "";
        public bool TieneUbicacion { get; set; }
    }

    public class BeneficiarioMapaDto
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = "";
        public string Domicilio { get; set; } = "";
        public string Localidad { get; set; } = "";
        public double Latitud { get; set; }
        public double Longitud { get; set; }
        public bool UbicacionAproximada { get; set; }
        public int PuntajeCriticidad { get; set; }
        public string ColorSemaforo { get; set; } = "";
        public int CantidadConvivientes { get; set; }
        public int CantidadHijosMenores { get; set; }
        public bool TieneIntegrantesDiscapacidad { get; set; }
    }

    public class MapaRiesgoDto
    {
        public List<BeneficiarioMapaDto> Ubicados { get; set; } = new();
        public int SinUbicar { get; set; }
        public int PuntajeMaximo { get; set; }
    }

    public class GeocodificacionDto
    {
        public double Latitud { get; set; }
        public double Longitud { get; set; }
        public string DireccionEncontrada { get; set; } = "";
        public bool Aproximada { get; set; }
    }

    public class ResultadoLoteGeocodificacion
    {
        public int Procesados { get; set; }
        public int Ubicados { get; set; }
        public int Aproximados { get; set; }
        public int NoEncontrados { get; set; }
        public int Restantes { get; set; }
    }
}
