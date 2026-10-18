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
    [Authorize]
    public class BeneficiariosController : ControllerBase
    {
        private const string ColumnasDetalle = @"Id, FechaRelevamiento, Cuil, NumeroDocumento, Apellido, Nombre, FechaNacimiento, Genero, EstadoCivil,
            TelefonoCelular, Email, Domicilio, Localidad, Provincia, CodigoPostal,
            NivelEducativo, CondicionLaboral, IngresosMensuales, PoseePlanSocial, OrganismoPlanSocial,
            CantidadConvivientes, CantidadHijosMenores, CantidadAdultosMayores, TieneIntegrantesDiscapacidad,
            TipoViviendaOriginal, RegimenTenenciaTerreno, MaterialPredominantePiso, MaterialPredominanteTecho,
            ProcedenciaAgua, DesagueInodoro, PuntajeCriticidad, ColorSemaforo, Latitud, Longitud, UbicacionAproximada";

        private const string SetCampos = @"Cuil = @Cuil, NumeroDocumento = @NumeroDocumento,
            Apellido = @Apellido, Nombre = @Nombre, FechaNacimiento = @FechaNacimiento, Genero = @Genero, EstadoCivil = @EstadoCivil,
            TelefonoCelular = @TelefonoCelular, Email = @Email, Domicilio = @Domicilio, Localidad = @Localidad, Provincia = @Provincia, CodigoPostal = @CodigoPostal,
            NivelEducativo = @NivelEducativo, CondicionLaboral = @CondicionLaboral, IngresosMensuales = @IngresosMensuales, PoseePlanSocial = @PoseePlanSocial, OrganismoPlanSocial = @OrganismoPlanSocial,
            CantidadConvivientes = @CantidadConvivientes, CantidadHijosMenores = @CantidadHijosMenores, CantidadAdultosMayores = @CantidadAdultosMayores, TieneIntegrantesDiscapacidad = @TieneIntegrantesDiscapacidad,
            TipoViviendaOriginal = @TipoViviendaOriginal, RegimenTenenciaTerreno = @RegimenTenenciaTerreno, MaterialPredominantePiso = @MaterialPredominantePiso, MaterialPredominanteTecho = @MaterialPredominanteTecho,
            ProcedenciaAgua = @ProcedenciaAgua, DesagueInodoro = @DesagueInodoro, PuntajeCriticidad = @PuntajeCriticidad, ColorSemaforo = @ColorSemaforo,
            Latitud = @Latitud, Longitud = @Longitud, UbicacionAproximada = @UbicacionAproximada";

        private readonly Db _db;
        private readonly AlgoritmoPriorizacion _algoritmo;
        private readonly GeocodificacionService _geo;

        public BeneficiariosController(Db db, AlgoritmoPriorizacion algoritmo, GeocodificacionService geo)
        {
            _db = db;
            _algoritmo = algoritmo;
            _geo = geo;
        }

        [HttpGet]
        public ActionResult<List<BeneficiarioListaDto>> GetBeneficiarios()
        {
            var lista = new List<BeneficiarioListaDto>();

            const string query = @"SELECT Id, FechaRelevamiento, Apellido, Nombre, NumeroDocumento, Domicilio, Localidad,
                                          PuntajeCriticidad, ColorSemaforo, Latitud
                                   FROM Beneficiarios WHERE Activo = 1 ORDER BY Apellido, Nombre";

            using var conexion = _db.Abrir();
            using var comando = new SqlCommand(query, conexion);
            using var reader = comando.ExecuteReader();
            while (reader.Read())
            {
                lista.Add(new BeneficiarioListaDto
                {
                    Id = reader.Entero("Id"),
                    FechaRelevamiento = reader.Fecha("FechaRelevamiento"),
                    Nombre = $"{reader.Texto("Apellido")}, {reader.Texto("Nombre")}",
                    Dni = reader.Texto("NumeroDocumento"),
                    Domicilio = reader.Texto("Domicilio"),
                    Localidad = reader.Texto("Localidad"),
                    PuntajeCriticidad = reader.Entero("PuntajeCriticidad"),
                    ColorSemaforo = reader.Texto("ColorSemaforo"),
                    TieneUbicacion = reader.Coordenada("Latitud").HasValue
                });
            }
            return Ok(lista);
        }

        [HttpGet("{id:int}")]
        public ActionResult<BeneficiarioDto> GetBeneficiarioById(int id)
        {
            using var conexion = _db.Abrir();
            var beneficiario = BuscarPorId(conexion, id);
            if (beneficiario == null)
                return NotFound(new { mensaje = "Beneficiario no encontrado." });

            return Ok(beneficiario);
        }

        /// <summary>Datos para el Mapa de Riesgo: solo beneficiarios activos con coordenadas.</summary>
        [HttpGet("mapa")]
        public ActionResult<MapaRiesgoDto> GetMapa()
        {
            var resultado = new MapaRiesgoDto { PuntajeMaximo = AlgoritmoPriorizacion.PuntajeMaximo };

            const string query = @"SELECT Id, Apellido, Nombre, Domicilio, Localidad, Latitud, Longitud, UbicacionAproximada,
                                          PuntajeCriticidad, ColorSemaforo, CantidadConvivientes, CantidadHijosMenores, TieneIntegrantesDiscapacidad
                                   FROM Beneficiarios WHERE Activo = 1;";

            using var conexion = _db.Abrir();
            using var comando = new SqlCommand(query, conexion);
            using var reader = comando.ExecuteReader();
            while (reader.Read())
            {
                double? lat = reader.Coordenada("Latitud");
                double? lng = reader.Coordenada("Longitud");
                if (lat == null || lng == null)
                {
                    resultado.SinUbicar++;
                    continue;
                }

                resultado.Ubicados.Add(new BeneficiarioMapaDto
                {
                    Id = reader.Entero("Id"),
                    Nombre = $"{reader.Texto("Apellido")}, {reader.Texto("Nombre")}",
                    Domicilio = reader.Texto("Domicilio"),
                    Localidad = reader.Texto("Localidad"),
                    Latitud = lat.Value,
                    Longitud = lng.Value,
                    UbicacionAproximada = reader.Booleano("UbicacionAproximada"),
                    PuntajeCriticidad = reader.Entero("PuntajeCriticidad"),
                    ColorSemaforo = reader.Texto("ColorSemaforo"),
                    CantidadConvivientes = reader.Entero("CantidadConvivientes"),
                    CantidadHijosMenores = reader.Entero("CantidadHijosMenores"),
                    TieneIntegrantesDiscapacidad = reader.Booleano("TieneIntegrantesDiscapacidad")
                });
            }
            return Ok(resultado);
        }

        /// <summary>Busca las coordenadas de una dirección (botón "Ubicar en el mapa" del formulario).</summary>
        [HttpGet("geocodificar")]
        public async Task<ActionResult<GeocodificacionDto>> Geocodificar([FromQuery] string? domicilio, [FromQuery] string localidad, [FromQuery] string? provincia, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(localidad))
                return BadRequest(new { mensaje = "Indicá al menos la localidad." });

            var resultado = await _geo.BuscarAsync(domicilio ?? "", localidad, provincia, ct);
            if (resultado == null)
                return NotFound(new { mensaje = "No se encontró esa dirección en el mapa. Podés cargar las coordenadas a mano." });

            return Ok(resultado);
        }

        [HttpPost]
        public IActionResult CrearBeneficiario([FromBody] BeneficiarioDto nuevo)
        {
            string? error = ValidarReglas(nuevo);
            if (error != null) return BadRequest(new { mensaje = error });

            var (puntaje, color) = _algoritmo.Evaluar(nuevo);

            try
            {
                using var conexion = _db.Abrir();

                var existente = BuscarPorIdentidad(conexion, nuevo.NumeroDocumento, nuevo.Cuil, excluirId: 0);
                if (existente is { Activo: true })
                    return Conflict(new { mensaje = $"Ya existe un beneficiario activo con ese DNI o CUIL: {existente.Value.Nombre}." });

                if (existente is { Activo: false })
                {
                    // La persona había sido dada de baja: se reactiva con los datos del nuevo relevamiento.
                    using var reactivar = new SqlCommand($"UPDATE Beneficiarios SET {SetCampos}, Activo = 1, FechaBaja = NULL WHERE Id = @Id", conexion);
                    CargarParametros(reactivar, nuevo, puntaje, color);
                    reactivar.Parameters.AddWithValue("@Id", existente.Value.Id);
                    reactivar.ExecuteNonQuery();

                    return Ok(new
                    {
                        id = existente.Value.Id,
                        mensaje = "Esta persona estaba dada de baja: se reactivó con los datos del nuevo relevamiento.",
                        puntosCalculados = puntaje,
                        prioridadAsignada = color
                    });
                }

                const string insertar = @"INSERT INTO Beneficiarios
                    (Cuil, NumeroDocumento, Apellido, Nombre, FechaNacimiento, Genero, EstadoCivil, TelefonoCelular, Email, Domicilio, Localidad, Provincia, CodigoPostal,
                     NivelEducativo, CondicionLaboral, IngresosMensuales, PoseePlanSocial, OrganismoPlanSocial, CantidadConvivientes, CantidadHijosMenores, CantidadAdultosMayores, TieneIntegrantesDiscapacidad,
                     TipoViviendaOriginal, RegimenTenenciaTerreno, MaterialPredominantePiso, MaterialPredominanteTecho, ProcedenciaAgua, DesagueInodoro, PuntajeCriticidad, ColorSemaforo,
                     Latitud, Longitud, UbicacionAproximada, Activo)
                    VALUES
                    (@Cuil, @NumeroDocumento, @Apellido, @Nombre, @FechaNacimiento, @Genero, @EstadoCivil, @TelefonoCelular, @Email, @Domicilio, @Localidad, @Provincia, @CodigoPostal,
                     @NivelEducativo, @CondicionLaboral, @IngresosMensuales, @PoseePlanSocial, @OrganismoPlanSocial, @CantidadConvivientes, @CantidadHijosMenores, @CantidadAdultosMayores, @TieneIntegrantesDiscapacidad,
                     @TipoViviendaOriginal, @RegimenTenenciaTerreno, @MaterialPredominantePiso, @MaterialPredominanteTecho, @ProcedenciaAgua, @DesagueInodoro, @PuntajeCriticidad, @ColorSemaforo,
                     @Latitud, @Longitud, @UbicacionAproximada, 1);
                    SELECT CAST(SCOPE_IDENTITY() AS INT);";

                using var comando = new SqlCommand(insertar, conexion);
                CargarParametros(comando, nuevo, puntaje, color);
                int id = (int)comando.ExecuteScalar();

                return Ok(new { id, mensaje = "Relevamiento guardado con éxito.", puntosCalculados = puntaje, prioridadAsignada = color });
            }
            catch (SqlException ex) when (ex.EsClaveDuplicada())
            {
                return Conflict(new { mensaje = "Ya existe un beneficiario con ese DNI o CUIL." });
            }
        }

        [HttpPut("{id:int}")]
        public IActionResult ActualizarBeneficiario(int id, [FromBody] BeneficiarioDto modificado)
        {
            string? error = ValidarReglas(modificado);
            if (error != null) return BadRequest(new { mensaje = error });

            var (puntaje, color) = _algoritmo.Evaluar(modificado);

            try
            {
                using var conexion = _db.Abrir();

                var otro = BuscarPorIdentidad(conexion, modificado.NumeroDocumento, modificado.Cuil, excluirId: id);
                if (otro != null)
                    return Conflict(new { mensaje = $"El DNI o CUIL ya pertenece a otro beneficiario: {otro.Value.Nombre}." });

                using var comando = new SqlCommand($"UPDATE Beneficiarios SET {SetCampos} WHERE Id = @Id AND Activo = 1", conexion);
                CargarParametros(comando, modificado, puntaje, color);
                comando.Parameters.AddWithValue("@Id", id);

                if (comando.ExecuteNonQuery() > 0)
                    return Ok(new { id, mensaje = "Relevamiento actualizado con éxito.", puntosCalculados = puntaje, prioridadAsignada = color });

                return NotFound(new { mensaje = "No se encontró el beneficiario para actualizar." });
            }
            catch (SqlException ex) when (ex.EsClaveDuplicada())
            {
                return Conflict(new { mensaje = "El DNI o CUIL ya pertenece a otro beneficiario." });
            }
        }

        /// <summary>
        /// Baja lógica (igual que Usuarios): el registro queda en la base con Activo = 0 y fecha de baja,
        /// así no se pierde el historial del relevamiento.
        /// </summary>
        [HttpDelete("{id:int}")]
        public IActionResult EliminarBeneficiario(int id)
        {
            using var conexion = _db.Abrir();
            using var comando = new SqlCommand("UPDATE Beneficiarios SET Activo = 0, FechaBaja = SYSDATETIME() WHERE Id = @Id AND Activo = 1", conexion);
            comando.Parameters.AddWithValue("@Id", id);

            if (comando.ExecuteNonQuery() > 0)
                return Ok(new { mensaje = "Beneficiario dado de baja del padrón." });

            return NotFound(new { mensaje = "No se encontró el beneficiario (o ya estaba dado de baja)." });
        }

        /// <summary>
        /// Ubica en el mapa a los beneficiarios que todavía no tienen coordenadas (de a tandas, por el límite de Nominatim).
        /// </summary>
        [HttpPost("geocodificar-pendientes")]
        public async Task<ActionResult<ResultadoLoteGeocodificacion>> GeocodificarPendientes([FromQuery] int maximo = 15, CancellationToken ct = default)
        {
            maximo = Math.Clamp(maximo, 1, 30);
            var resultado = new ResultadoLoteGeocodificacion();
            var pendientes = new List<(int Id, string Domicilio, string Localidad, string Provincia)>();

            using var conexion = _db.Abrir();
            using (var leer = new SqlCommand(@"SELECT TOP (@Max) Id, Domicilio, Localidad, Provincia FROM Beneficiarios
                                               WHERE Activo = 1 AND (Latitud IS NULL OR Longitud IS NULL)
                                               ORDER BY PuntajeCriticidad DESC", conexion))
            {
                leer.Parameters.AddWithValue("@Max", maximo);
                using var reader = leer.ExecuteReader();
                while (reader.Read())
                    pendientes.Add((reader.Entero("Id"), reader.Texto("Domicilio"), reader.Texto("Localidad"), reader.Texto("Provincia")));
            }

            foreach (var p in pendientes)
            {
                if (ct.IsCancellationRequested) break;
                resultado.Procesados++;

                var ubicacion = await _geo.BuscarAsync(p.Domicilio, p.Localidad, p.Provincia, ct);
                if (ubicacion == null)
                {
                    resultado.NoEncontrados++;
                    continue;
                }

                using var actualizar = new SqlCommand("UPDATE Beneficiarios SET Latitud = @Lat, Longitud = @Lng, UbicacionAproximada = @Aprox WHERE Id = @Id", conexion);
                actualizar.Parameters.AddWithValue("@Lat", ubicacion.Latitud);
                actualizar.Parameters.AddWithValue("@Lng", ubicacion.Longitud);
                actualizar.Parameters.AddWithValue("@Aprox", ubicacion.Aproximada);
                actualizar.Parameters.AddWithValue("@Id", p.Id);
                actualizar.ExecuteNonQuery();

                if (ubicacion.Aproximada) resultado.Aproximados++;
                else resultado.Ubicados++;
            }

            using (var contar = new SqlCommand("SELECT COUNT(1) FROM Beneficiarios WHERE Activo = 1 AND (Latitud IS NULL OR Longitud IS NULL)", conexion))
                resultado.Restantes = Convert.ToInt32(contar.ExecuteScalar());

            return Ok(resultado);
        }

        // ---------------------------------------------------------------- helpers

        private static string? ValidarReglas(BeneficiarioDto b)
        {
            if (b.FechaNacimiento > DateTime.Today)
                return "La fecha de nacimiento no puede ser futura.";
            if (b.FechaNacimiento < DateTime.Today.AddYears(-120))
                return "La fecha de nacimiento no es válida.";
            if (!string.IsNullOrWhiteSpace(b.Email) && !b.Email.Contains('@'))
                return "El email no tiene un formato válido.";
            if (b.CantidadHijosMenores + b.CantidadAdultosMayores > b.CantidadConvivientes)
                return "Hijos menores + adultos mayores no puede superar el total de integrantes del hogar.";
            if (b.Latitud.HasValue != b.Longitud.HasValue)
                return "Cargá latitud y longitud juntas (o ninguna).";
            return null;
        }

        private static (int Id, bool Activo, string Nombre)? BuscarPorIdentidad(SqlConnection conexion, string dni, string cuil, int excluirId)
        {
            const string query = @"SELECT TOP 1 Id, Activo, Apellido, Nombre FROM Beneficiarios
                                   WHERE (NumeroDocumento = @Dni OR Cuil = @Cuil) AND Id <> @Excluir
                                   ORDER BY Activo DESC";
            using var comando = new SqlCommand(query, conexion);
            comando.Parameters.AddWithValue("@Dni", dni.Trim());
            comando.Parameters.AddWithValue("@Cuil", cuil.Trim());
            comando.Parameters.AddWithValue("@Excluir", excluirId);
            using var reader = comando.ExecuteReader();
            if (!reader.Read()) return null;
            return (reader.Entero("Id"), reader.Booleano("Activo"), $"{reader.Texto("Apellido")}, {reader.Texto("Nombre")}");
        }

        private static BeneficiarioDto? BuscarPorId(SqlConnection conexion, int id)
        {
            using var comando = new SqlCommand($"SELECT {ColumnasDetalle} FROM Beneficiarios WHERE Id = @Id AND Activo = 1", conexion);
            comando.Parameters.AddWithValue("@Id", id);
            using var reader = comando.ExecuteReader();
            return reader.Read() ? LeerDetalle(reader) : null;
        }

        private static BeneficiarioDto LeerDetalle(SqlDataReader r) => new()
        {
            Id = r.Entero("Id"),
            FechaRelevamiento = r.Fecha("FechaRelevamiento"),
            Cuil = r.Texto("Cuil"),
            NumeroDocumento = r.Texto("NumeroDocumento"),
            Apellido = r.Texto("Apellido"),
            Nombre = r.Texto("Nombre"),
            FechaNacimiento = r.Fecha("FechaNacimiento"),
            Genero = r.Texto("Genero"),
            EstadoCivil = r.Texto("EstadoCivil"),
            TelefonoCelular = r.Texto("TelefonoCelular"),
            Email = r.Texto("Email"),
            Domicilio = r.Texto("Domicilio"),
            Localidad = r.Texto("Localidad"),
            Provincia = r.Texto("Provincia"),
            CodigoPostal = r.Texto("CodigoPostal"),
            NivelEducativo = r.Texto("NivelEducativo"),
            CondicionLaboral = r.Texto("CondicionLaboral"),
            IngresosMensuales = r.Decimal("IngresosMensuales"),
            PoseePlanSocial = r.Booleano("PoseePlanSocial"),
            OrganismoPlanSocial = r.Texto("OrganismoPlanSocial"),
            CantidadConvivientes = r.Entero("CantidadConvivientes"),
            CantidadHijosMenores = r.Entero("CantidadHijosMenores"),
            CantidadAdultosMayores = r.Entero("CantidadAdultosMayores"),
            TieneIntegrantesDiscapacidad = r.Booleano("TieneIntegrantesDiscapacidad"),
            TipoViviendaOriginal = r.Texto("TipoViviendaOriginal"),
            RegimenTenenciaTerreno = r.Texto("RegimenTenenciaTerreno"),
            MaterialPredominantePiso = r.Texto("MaterialPredominantePiso"),
            MaterialPredominanteTecho = r.Texto("MaterialPredominanteTecho"),
            ProcedenciaAgua = r.Texto("ProcedenciaAgua"),
            DesagueInodoro = r.Texto("DesagueInodoro"),
            PuntajeCriticidad = r.Entero("PuntajeCriticidad"),
            ColorSemaforo = r.Texto("ColorSemaforo"),
            Latitud = r.Coordenada("Latitud"),
            Longitud = r.Coordenada("Longitud"),
            UbicacionAproximada = r.Booleano("UbicacionAproximada")
        };

        private static void CargarParametros(SqlCommand cmd, BeneficiarioDto b, int puntaje, string color)
        {
            var p = cmd.Parameters;
            p.AddWithValue("@Cuil", b.Cuil.Trim());
            p.AddWithValue("@NumeroDocumento", b.NumeroDocumento.Trim());
            p.AddWithValue("@Apellido", b.Apellido.Trim());
            p.AddWithValue("@Nombre", b.Nombre.Trim());
            p.AddWithValue("@FechaNacimiento", b.FechaNacimiento!.Value.Date);
            p.AddWithValue("@Genero", b.Genero);
            p.AddWithValue("@EstadoCivil", b.EstadoCivil);
            p.AddWithValue("@TelefonoCelular", b.TelefonoCelular?.Trim() ?? "");
            p.AddWithValue("@Email", b.Email?.Trim() ?? "");
            p.AddWithValue("@Domicilio", b.Domicilio.Trim());
            p.AddWithValue("@Localidad", b.Localidad.Trim());
            p.AddWithValue("@Provincia", string.IsNullOrWhiteSpace(b.Provincia) ? "Tucumán" : b.Provincia.Trim());
            p.AddWithValue("@CodigoPostal", b.CodigoPostal.Trim());
            p.AddWithValue("@NivelEducativo", b.NivelEducativo);
            p.AddWithValue("@CondicionLaboral", b.CondicionLaboral);
            p.AddWithValue("@IngresosMensuales", b.IngresosMensuales);
            p.AddWithValue("@PoseePlanSocial", b.PoseePlanSocial);
            p.AddWithValue("@OrganismoPlanSocial", b.PoseePlanSocial ? b.OrganismoPlanSocial ?? "" : "");
            p.AddWithValue("@CantidadConvivientes", b.CantidadConvivientes);
            p.AddWithValue("@CantidadHijosMenores", b.CantidadHijosMenores);
            p.AddWithValue("@CantidadAdultosMayores", b.CantidadAdultosMayores);
            p.AddWithValue("@TieneIntegrantesDiscapacidad", b.TieneIntegrantesDiscapacidad);
            p.AddWithValue("@TipoViviendaOriginal", b.TipoViviendaOriginal);
            p.AddWithValue("@RegimenTenenciaTerreno", b.RegimenTenenciaTerreno);
            p.AddWithValue("@MaterialPredominantePiso", b.MaterialPredominantePiso);
            p.AddWithValue("@MaterialPredominanteTecho", b.MaterialPredominanteTecho);
            p.AddWithValue("@ProcedenciaAgua", b.ProcedenciaAgua);
            p.AddWithValue("@DesagueInodoro", b.DesagueInodoro);
            p.AddWithValue("@PuntajeCriticidad", puntaje);
            p.AddWithValue("@ColorSemaforo", color);
            p.AddWithValue("@Latitud", b.Latitud.ONull());
            p.AddWithValue("@Longitud", b.Longitud.ONull());
            p.AddWithValue("@UbicacionAproximada", b.Latitud.HasValue && b.UbicacionAproximada);
        }
    }
}
