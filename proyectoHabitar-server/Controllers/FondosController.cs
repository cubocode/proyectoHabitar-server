using System.Data.SqlClient;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using proyectoHabitar_server.Datos;
using proyectoHabitar_server.Modelos;
using proyectoHabitar_server.Servicios;

namespace proyectoHabitar_server.Controllers
{
    /// <summary>
    /// Caja de fondos: ingresos y egresos de dinero.
    /// Los movimientos no se borran nunca: se anulan con motivo, usuario y fecha (queda la auditoría).
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = Roles.Administrador)]
    public class FondosController : ControllerBase
    {
        private const string Columnas = @"Id, FechaHora, Tipo, Categoria, Concepto, Detalle, Monto, MedioPago, Comprobante,
                                          UsuarioNombre, FechaRegistro, Anulado, MotivoAnulacion, FechaAnulacion, UsuarioAnulacion";

        private static readonly string[] CategoriasIngreso =
            { "Aporte provincial", "Aporte nacional", "Aporte municipal", "Donación", "Subsidio", "Otros ingresos" };

        private static readonly string[] CategoriasEgreso =
            { "Materiales de construcción", "Mano de obra", "Ayuda directa a familias", "Logística y transporte", "Gastos administrativos", "Otros egresos" };

        private readonly Db _db;

        public FondosController(Db db)
        {
            _db = db;
        }

        /// <summary>Lista de movimientos con filtros. "hasta" es inclusivo (todo ese día).</summary>
        [HttpGet("movimientos")]
        public ActionResult<List<MovimientoDto>> GetMovimientos(
            [FromQuery] DateTime? desde, [FromQuery] DateTime? hasta, [FromQuery] string? tipo,
            [FromQuery] string? categoria, [FromQuery] string? texto, [FromQuery] bool incluirAnulados = false)
        {
            var lista = new List<MovimientoDto>();

            using var conexion = _db.Abrir();
            using var comando = new SqlCommand { Connection = conexion };

            var where = new StringBuilder("WHERE 1 = 1");
            if (!incluirAnulados) where.Append(" AND Anulado = 0");
            AgregarFiltroFechas(comando, where, desde, hasta);

            if (!string.IsNullOrWhiteSpace(tipo))
            {
                where.Append(" AND Tipo = @Tipo");
                comando.Parameters.AddWithValue("@Tipo", tipo);
            }
            if (!string.IsNullOrWhiteSpace(categoria))
            {
                where.Append(" AND Categoria = @Categoria");
                comando.Parameters.AddWithValue("@Categoria", categoria);
            }
            if (!string.IsNullOrWhiteSpace(texto))
            {
                where.Append(" AND (Concepto LIKE @Texto OR Detalle LIKE @Texto OR Comprobante LIKE @Texto OR UsuarioNombre LIKE @Texto)");
                comando.Parameters.AddWithValue("@Texto", $"%{texto.Trim()}%");
            }

            comando.CommandText = $"SELECT {Columnas} FROM MovimientosFondos {where} ORDER BY FechaHora DESC, Id DESC";
            using var reader = comando.ExecuteReader();
            while (reader.Read())
                lista.Add(Leer(reader));

            return Ok(lista);
        }

        [HttpGet("movimientos/{id:int}")]
        public ActionResult<MovimientoDto> GetMovimiento(int id)
        {
            using var conexion = _db.Abrir();
            var movimiento = BuscarPorId(conexion, id);
            return movimiento == null ? NotFound(new { mensaje = "Movimiento no encontrado." }) : Ok(movimiento);
        }

        /// <summary>Totales del período, saldo anterior y saldo actual de la caja (solo movimientos no anulados).</summary>
        [HttpGet("resumen")]
        public ActionResult<ResumenFondosDto> GetResumen([FromQuery] DateTime? desde, [FromQuery] DateTime? hasta)
        {
            var resumen = new ResumenFondosDto();
            using var conexion = _db.Abrir();

            resumen.SaldoActual = Saldo(conexion);

            if (desde.HasValue)
            {
                using var anterior = new SqlCommand(@"SELECT ISNULL(SUM(CASE WHEN Tipo = 'Ingreso' THEN Monto ELSE -Monto END), 0)
                                                      FROM MovimientosFondos WHERE Anulado = 0 AND FechaHora < @Desde", conexion);
                anterior.Parameters.AddWithValue("@Desde", desde.Value.Date);
                resumen.SaldoAnterior = Convert.ToDecimal(anterior.ExecuteScalar());
            }

            using var comando = new SqlCommand { Connection = conexion };
            var where = new StringBuilder("WHERE Anulado = 0");
            AgregarFiltroFechas(comando, where, desde, hasta);
            comando.CommandText = $@"SELECT Tipo, Categoria, SUM(Monto) AS Total, COUNT(1) AS Cantidad
                                     FROM MovimientosFondos {where}
                                     GROUP BY Tipo, Categoria ORDER BY Tipo DESC, Total DESC";

            using (var reader = comando.ExecuteReader())
            {
                while (reader.Read())
                {
                    var fila = new TotalCategoriaDto
                    {
                        Tipo = reader.Texto("Tipo"),
                        Categoria = reader.Texto("Categoria"),
                        Total = reader.Decimal("Total"),
                        Cantidad = reader.Entero("Cantidad")
                    };
                    resumen.PorCategoria.Add(fila);
                    resumen.CantidadMovimientos += fila.Cantidad;
                    if (fila.Tipo == TiposMovimiento.Ingreso) resumen.TotalIngresos += fila.Total;
                    else resumen.TotalEgresos += fila.Total;
                }
            }

            resumen.SaldoPeriodo = resumen.TotalIngresos - resumen.TotalEgresos;
            resumen.SaldoFinal = resumen.SaldoAnterior + resumen.SaldoPeriodo;
            return Ok(resumen);
        }

        /// <summary>Categorías sugeridas más las que ya se usaron, por tipo.</summary>
        [HttpGet("categorias")]
        public ActionResult<Dictionary<string, List<string>>> GetCategorias()
        {
            var ingreso = new SortedSet<string>(CategoriasIngreso, StringComparer.CurrentCultureIgnoreCase);
            var egreso = new SortedSet<string>(CategoriasEgreso, StringComparer.CurrentCultureIgnoreCase);

            using var conexion = _db.Abrir();
            using var comando = new SqlCommand("SELECT DISTINCT Tipo, Categoria FROM MovimientosFondos", conexion);
            using var reader = comando.ExecuteReader();
            while (reader.Read())
            {
                if (reader.Texto("Tipo") == TiposMovimiento.Ingreso) ingreso.Add(reader.Texto("Categoria"));
                else egreso.Add(reader.Texto("Categoria"));
            }

            return Ok(new Dictionary<string, List<string>>
            {
                [TiposMovimiento.Ingreso] = ingreso.ToList(),
                [TiposMovimiento.Egreso] = egreso.ToList()
            });
        }

        [HttpPost("movimientos")]
        public IActionResult CrearMovimiento([FromBody] MovimientoGuardarDto m)
        {
            string? error = ValidarFecha(m);
            if (error != null) return BadRequest(new { mensaje = error });

            using var conexion = _db.Abrir();
            using var transaccion = conexion.BeginTransaction(System.Data.IsolationLevel.Serializable);

            if (m.Tipo == TiposMovimiento.Egreso)
            {
                decimal disponible = Saldo(conexion, transaccion);
                if (m.Monto > disponible)
                    return Conflict(new { mensaje = $"Saldo insuficiente. Disponible en caja: {Pesos(disponible)}." });
            }

            const string query = @"INSERT INTO MovimientosFondos
                (FechaHora, Tipo, Categoria, Concepto, Detalle, Monto, MedioPago, Comprobante, UsuarioId, UsuarioNombre)
                VALUES (@FechaHora, @Tipo, @Categoria, @Concepto, @Detalle, @Monto, @MedioPago, @Comprobante, @UsuarioId, @UsuarioNombre);
                SELECT CAST(SCOPE_IDENTITY() AS INT);";

            using var comando = new SqlCommand(query, conexion, transaccion);
            CargarParametros(comando, m);
            comando.Parameters.AddWithValue("@UsuarioId", User.IdUsuario());
            comando.Parameters.AddWithValue("@UsuarioNombre", User.NombreParaAuditoria());
            int id = (int)comando.ExecuteScalar();
            transaccion.Commit();

            return Ok(new { id, mensaje = $"{m.Tipo} registrado correctamente." });
        }

        [HttpPut("movimientos/{id:int}")]
        public IActionResult ModificarMovimiento(int id, [FromBody] MovimientoGuardarDto m)
        {
            string? error = ValidarFecha(m);
            if (error != null) return BadRequest(new { mensaje = error });

            using var conexion = _db.Abrir();
            using var transaccion = conexion.BeginTransaction(System.Data.IsolationLevel.Serializable);

            var actual = BuscarPorId(conexion, id, transaccion);
            if (actual == null) return NotFound(new { mensaje = "Movimiento no encontrado." });
            if (actual.Anulado) return Conflict(new { mensaje = "No se puede modificar un movimiento anulado." });

            decimal saldoSinEste = Saldo(conexion, transaccion, excluirId: id);
            decimal saldoResultante = saldoSinEste + (m.Tipo == TiposMovimiento.Ingreso ? m.Monto : -m.Monto);
            if (saldoResultante < 0)
                return Conflict(new { mensaje = $"El cambio dejaría la caja en negativo ({Pesos(saldoResultante)})." });

            const string query = @"UPDATE MovimientosFondos SET FechaHora = @FechaHora, Tipo = @Tipo, Categoria = @Categoria, Concepto = @Concepto,
                                   Detalle = @Detalle, Monto = @Monto, MedioPago = @MedioPago, Comprobante = @Comprobante
                                   WHERE Id = @Id AND Anulado = 0";
            using var comando = new SqlCommand(query, conexion, transaccion);
            CargarParametros(comando, m);
            comando.Parameters.AddWithValue("@Id", id);
            comando.ExecuteNonQuery();
            transaccion.Commit();

            return Ok(new { id, mensaje = "Movimiento actualizado." });
        }

        [HttpPost("movimientos/{id:int}/anular")]
        public IActionResult AnularMovimiento(int id, [FromBody] AnularMovimientoDto datos)
        {
            using var conexion = _db.Abrir();
            using var transaccion = conexion.BeginTransaction(System.Data.IsolationLevel.Serializable);

            var actual = BuscarPorId(conexion, id, transaccion);
            if (actual == null) return NotFound(new { mensaje = "Movimiento no encontrado." });
            if (actual.Anulado) return Conflict(new { mensaje = "El movimiento ya estaba anulado." });

            if (actual.Tipo == TiposMovimiento.Ingreso)
            {
                decimal saldo = Saldo(conexion, transaccion);
                if (saldo - actual.Monto < 0)
                    return Conflict(new { mensaje = $"No se puede anular: la caja quedaría en negativo ({Pesos(saldo - actual.Monto)})." });
            }

            using var comando = new SqlCommand(@"UPDATE MovimientosFondos
                                                 SET Anulado = 1, MotivoAnulacion = @Motivo, FechaAnulacion = SYSDATETIME(), UsuarioAnulacion = @Usuario
                                                 WHERE Id = @Id", conexion, transaccion);
            comando.Parameters.AddWithValue("@Motivo", datos.Motivo.Trim());
            comando.Parameters.AddWithValue("@Usuario", User.NombreParaAuditoria());
            comando.Parameters.AddWithValue("@Id", id);
            comando.ExecuteNonQuery();
            transaccion.Commit();

            return Ok(new { mensaje = "Movimiento anulado." });
        }

        // ---------------------------------------------------------------- helpers

        private static readonly System.Globalization.CultureInfo CulturaAR = new("es-AR");
        private static string Pesos(decimal monto) => monto.ToString("C2", CulturaAR);

        private static string? ValidarFecha(MovimientoGuardarDto m)
        {
            if (m.FechaHora > DateTime.Now.AddMinutes(10))
                return "No se pueden registrar movimientos con fecha futura.";
            if (m.FechaHora < new DateTime(2000, 1, 1))
                return "La fecha del movimiento no es válida.";
            return null;
        }

        private static void AgregarFiltroFechas(SqlCommand comando, StringBuilder where, DateTime? desde, DateTime? hasta)
        {
            if (desde.HasValue)
            {
                where.Append(" AND FechaHora >= @Desde");
                comando.Parameters.AddWithValue("@Desde", desde.Value.Date);
            }
            if (hasta.HasValue)
            {
                where.Append(" AND FechaHora < @HastaExclusivo");
                comando.Parameters.AddWithValue("@HastaExclusivo", hasta.Value.Date.AddDays(1));
            }
        }

        private static decimal Saldo(SqlConnection conexion, SqlTransaction? transaccion = null, int excluirId = 0)
        {
            using var comando = new SqlCommand(@"SELECT ISNULL(SUM(CASE WHEN Tipo = 'Ingreso' THEN Monto ELSE -Monto END), 0)
                                                 FROM MovimientosFondos WHERE Anulado = 0 AND Id <> @Excluir", conexion, transaccion);
            comando.Parameters.AddWithValue("@Excluir", excluirId);
            return Convert.ToDecimal(comando.ExecuteScalar());
        }

        private static MovimientoDto? BuscarPorId(SqlConnection conexion, int id, SqlTransaction? transaccion = null)
        {
            using var comando = new SqlCommand($"SELECT {Columnas} FROM MovimientosFondos WHERE Id = @Id", conexion, transaccion);
            comando.Parameters.AddWithValue("@Id", id);
            using var reader = comando.ExecuteReader();
            return reader.Read() ? Leer(reader) : null;
        }

        private static void CargarParametros(SqlCommand cmd, MovimientoGuardarDto m)
        {
            cmd.Parameters.AddWithValue("@FechaHora", m.FechaHora!.Value);
            cmd.Parameters.AddWithValue("@Tipo", m.Tipo);
            cmd.Parameters.AddWithValue("@Categoria", m.Categoria.Trim());
            cmd.Parameters.AddWithValue("@Concepto", m.Concepto.Trim());
            cmd.Parameters.AddWithValue("@Detalle", m.Detalle.ONull());
            cmd.Parameters.AddWithValue("@Monto", Math.Round(m.Monto, 2));
            cmd.Parameters.AddWithValue("@MedioPago", m.MedioPago.Trim());
            cmd.Parameters.AddWithValue("@Comprobante", m.Comprobante.ONull());
        }

        private static MovimientoDto Leer(SqlDataReader r) => new()
        {
            Id = r.Entero("Id"),
            FechaHora = r.Fecha("FechaHora") ?? DateTime.MinValue,
            Tipo = r.Texto("Tipo"),
            Categoria = r.Texto("Categoria"),
            Concepto = r.Texto("Concepto"),
            Detalle = r.Texto("Detalle"),
            Monto = r.Decimal("Monto"),
            MedioPago = r.Texto("MedioPago"),
            Comprobante = r.Texto("Comprobante"),
            UsuarioNombre = r.Texto("UsuarioNombre"),
            FechaRegistro = r.Fecha("FechaRegistro"),
            Anulado = r.Booleano("Anulado"),
            MotivoAnulacion = r.Texto("MotivoAnulacion"),
            FechaAnulacion = r.Fecha("FechaAnulacion"),
            UsuarioAnulacion = r.Texto("UsuarioAnulacion")
        };
    }
}
