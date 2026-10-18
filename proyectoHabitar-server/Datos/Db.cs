using System.Data.SqlClient;

namespace proyectoHabitar_server.Datos
{
    /// <summary>
    /// Punto único de acceso a la base. La cadena de conexión vive en appsettings.json
    /// (ConnectionStrings:HabitarBD) en lugar de estar repetida en cada controller.
    /// </summary>
    public class Db
    {
        private readonly string _connectionString;

        public Db(IConfiguration config)
        {
            _connectionString = config.GetConnectionString("HabitarBD")
                ?? throw new InvalidOperationException("Falta ConnectionStrings:HabitarBD en appsettings.json");
        }

        public SqlConnection Abrir()
        {
            var conexion = new SqlConnection(_connectionString);
            conexion.Open();
            return conexion;
        }
    }

    public static class SqlExtensiones
    {
        // Lecturas tolerantes a NULL: una columna vacía en la base ya no rompe el endpoint con un 500.
        public static string Texto(this SqlDataReader r, string columna)
        {
            int i = r.GetOrdinal(columna);
            return r.IsDBNull(i) ? "" : Convert.ToString(r.GetValue(i)) ?? "";
        }

        public static int Entero(this SqlDataReader r, string columna)
        {
            int i = r.GetOrdinal(columna);
            return r.IsDBNull(i) ? 0 : Convert.ToInt32(r.GetValue(i));
        }

        public static decimal Decimal(this SqlDataReader r, string columna)
        {
            int i = r.GetOrdinal(columna);
            return r.IsDBNull(i) ? 0m : Convert.ToDecimal(r.GetValue(i));
        }

        public static bool Booleano(this SqlDataReader r, string columna)
        {
            int i = r.GetOrdinal(columna);
            return !r.IsDBNull(i) && Convert.ToBoolean(r.GetValue(i));
        }

        public static DateTime? Fecha(this SqlDataReader r, string columna)
        {
            int i = r.GetOrdinal(columna);
            return r.IsDBNull(i) ? null : Convert.ToDateTime(r.GetValue(i));
        }

        public static double? Coordenada(this SqlDataReader r, string columna)
        {
            int i = r.GetOrdinal(columna);
            return r.IsDBNull(i) ? null : Convert.ToDouble(r.GetValue(i));
        }

        public static object ONull(this string? valor) =>
            string.IsNullOrWhiteSpace(valor) ? DBNull.Value : valor.Trim();

        public static object ONull(this double? valor) =>
            valor.HasValue ? valor.Value : DBNull.Value;

        /// <summary>Violación de índice/constraint UNIQUE (DNI, CUIL o usuario repetido).</summary>
        public static bool EsClaveDuplicada(this SqlException ex) => ex.Number is 2627 or 2601;
    }
}
