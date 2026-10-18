using proyectoHabitar_server.Modelos;

namespace proyectoHabitar_server.Servicios
{
    /// <summary>
    /// Calcula el puntaje de criticidad y el color del semáforo.
    ///
    /// Criterio                                         Puntos
    /// ------------------------------------------------ ------
    /// Hijos menores: 1 / 2 / 3 o más                   10 / 20 / 30
    /// Al menos un adulto mayor (+65)                   10
    /// Integrante con discapacidad                      25
    /// Vivienda: Rancho o Casilla / Pieza (inquilinato) 20 / 10
    /// Agua: pozo sin bomba, cisterna, lluvia o río     10
    /// Desagüe: hoyo / sin baño                         10
    /// Piso de tierra o ladrillo suelto                 10
    /// Techo: cartón, caña o paja / chapa simple        10 / 5
    /// Tenencia: ocupación informal / prestado          10 / 5
    /// Desempleo, o changa con ingresos bajos           15
    /// ------------------------------------------------ ------
    /// Máximo posible                                   150
    ///
    /// Los textos se comparan por palabras clave (sin importar mayúsculas), así funcionan tanto
    /// con las opciones actuales de los combos como con fichas viejas guardadas con otros textos
    /// (antes "Pieza ocupada en inquilinato" nunca sumaba porque se comparaba contra "Pieza").
    /// Umbrales en appsettings.json → Priorizacion.
    /// </summary>
    public class AlgoritmoPriorizacion
    {
        public const int PuntajeMaximo = 150;

        private readonly int _umbralRojo;
        private readonly int _umbralAmarillo;
        private readonly decimal _umbralIngresosBajos;

        public AlgoritmoPriorizacion(IConfiguration config)
        {
            _umbralRojo = config.GetValue<int?>("Priorizacion:UmbralRojo") ?? 80;
            _umbralAmarillo = config.GetValue<int?>("Priorizacion:UmbralAmarillo") ?? 45;
            _umbralIngresosBajos = config.GetValue<decimal?>("Priorizacion:UmbralIngresosBajos") ?? 100000m;
        }

        public (int Puntaje, string Color) Evaluar(BeneficiarioDto b)
        {
            int puntaje = 0;

            // Grupo familiar
            if (b.CantidadHijosMenores >= 3) puntaje += 30;
            else if (b.CantidadHijosMenores == 2) puntaje += 20;
            else if (b.CantidadHijosMenores == 1) puntaje += 10;

            if (b.CantidadAdultosMayores > 0) puntaje += 10;
            if (b.TieneIntegrantesDiscapacidad) puntaje += 25;

            // Vivienda e infraestructura
            if (Contiene(b.TipoViviendaOriginal, "rancho", "casilla")) puntaje += 20;
            else if (Contiene(b.TipoViviendaOriginal, "pieza", "inquilinato")) puntaje += 10;

            if (Contiene(b.ProcedenciaAgua, "pozo sin bomba", "cisterna", "lluvia", "río", "rio")) puntaje += 10;
            if (Contiene(b.DesagueInodoro, "hoyo", "no tiene")) puntaje += 10;
            if (Contiene(b.MaterialPredominantePiso, "tierra", "ladrillo suelto")) puntaje += 10;

            if (Contiene(b.MaterialPredominanteTecho, "cartón", "carton", "caña", "cana", "paja")) puntaje += 10;
            else if (Contiene(b.MaterialPredominanteTecho, "chapa de metal simple")) puntaje += 5;

            if (Contiene(b.RegimenTenenciaTerreno, "ocupación informal", "ocupacion informal")) puntaje += 10;
            else if (Contiene(b.RegimenTenenciaTerreno, "prestado", "cedido")) puntaje += 5;

            // Situación económica
            bool desempleado = Contiene(b.CondicionLaboral, "desemplead");
            bool changaConIngresosBajos = Contiene(b.CondicionLaboral, "informal", "changa") && b.IngresosMensuales < _umbralIngresosBajos;
            if (desempleado || changaConIngresosBajos) puntaje += 15;

            return (puntaje, Color(puntaje));
        }

        public string Color(int puntaje) =>
            puntaje >= _umbralRojo ? "Rojo" : (puntaje >= _umbralAmarillo ? "Amarillo" : "Verde");

        private static bool Contiene(string? valor, params string[] claves)
        {
            if (string.IsNullOrWhiteSpace(valor)) return false;
            string texto = valor.ToLowerInvariant();
            return claves.Any(c => texto.Contains(c));
        }
    }
}
