using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json.Serialization;
using proyectoHabitar_server.Modelos;

namespace proyectoHabitar_server.Servicios
{
    /// <summary>
    /// Convierte "domicilio + localidad" en coordenadas usando Nominatim (OpenStreetMap), gratis y sin API key.
    /// La política de uso pide como máximo 1 consulta por segundo y un User-Agent que identifique la app;
    /// por eso las consultas pasan de a una por un semáforo.
    /// Si la calle exacta no aparece (muy común en barrios y zonas rurales) se intenta con la localidad
    /// y el resultado se marca como "aproximado".
    /// </summary>
    public class GeocodificacionService
    {
        private static readonly SemaphoreSlim Turno = new(1, 1);
        private static DateTime _ultimaConsulta = DateTime.MinValue;

        private readonly HttpClient _http;
        private readonly ILogger<GeocodificacionService> _logger;

        public GeocodificacionService(HttpClient http, IConfiguration config, ILogger<GeocodificacionService> logger)
        {
            _http = http;
            _logger = logger;
            _http.BaseAddress = new Uri("https://nominatim.openstreetmap.org/");
            _http.Timeout = TimeSpan.FromSeconds(15);
            _http.DefaultRequestHeaders.UserAgent.ParseAdd(config["Geocodificacion:UserAgent"] ?? "HabitAR/1.0");
            _http.DefaultRequestHeaders.AcceptLanguage.Add(new StringWithQualityHeaderValue("es"));
        }

        public async Task<GeocodificacionDto?> BuscarAsync(string domicilio, string localidad, string? provincia, CancellationToken ct = default)
        {
            provincia = string.IsNullOrWhiteSpace(provincia) ? "Tucumán" : provincia;

            if (!string.IsNullOrWhiteSpace(domicilio))
            {
                var exacta = await ConsultarAsync($"{domicilio}, {localidad}, {provincia}, Argentina", ct);
                if (exacta != null) return exacta;
            }

            var aproximada = await ConsultarAsync($"{localidad}, {provincia}, Argentina", ct);
            if (aproximada != null) aproximada.Aproximada = true;
            return aproximada;
        }

        private async Task<GeocodificacionDto?> ConsultarAsync(string consulta, CancellationToken ct)
        {
            await Turno.WaitAsync(ct);
            try
            {
                var espera = _ultimaConsulta.AddMilliseconds(1100) - DateTime.UtcNow;
                if (espera > TimeSpan.Zero) await Task.Delay(espera, ct);

                string url = $"search?format=jsonv2&limit=1&countrycodes=ar&q={Uri.EscapeDataString(consulta)}";
                using var respuesta = await _http.GetAsync(url, ct);
                _ultimaConsulta = DateTime.UtcNow;

                if (!respuesta.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Nominatim respondió {Estado} para '{Consulta}'", respuesta.StatusCode, consulta);
                    return null;
                }

                var resultados = await respuesta.Content.ReadFromJsonAsync<List<ResultadoNominatim>>(cancellationToken: ct);
                var primero = resultados?.FirstOrDefault();
                if (primero == null) return null;

                return new GeocodificacionDto
                {
                    Latitud = double.Parse(primero.Lat, CultureInfo.InvariantCulture),
                    Longitud = double.Parse(primero.Lon, CultureInfo.InvariantCulture),
                    DireccionEncontrada = primero.DisplayName
                };
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                _logger.LogWarning(ex, "No se pudo consultar Nominatim para '{Consulta}'", consulta);
                return null;
            }
            finally
            {
                Turno.Release();
            }
        }

        private class ResultadoNominatim
        {
            [JsonPropertyName("lat")] public string Lat { get; set; } = "0";
            [JsonPropertyName("lon")] public string Lon { get; set; } = "0";
            [JsonPropertyName("display_name")] public string DisplayName { get; set; } = "";
        }
    }
}
