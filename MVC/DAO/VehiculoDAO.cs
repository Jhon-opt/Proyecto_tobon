using Npgsql;
using TransportesTobonApp.MVC.Models;

namespace TransportesTobonApp.MVC.DAO
{
    public class VehiculoDAO
    {
        private readonly string _connectionString;

        public VehiculoDAO(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection");
        }

        // HU-008: listado con filtro por placa y/o estado.
        // Servicios activos = registros en servicios sin fecha_fin_real.
        public async Task<List<Vehiculo>> Listar(string? filtro = null, string? estado = null)
        {
            var lista = new List<Vehiculo>();
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            string sql = @"
                SELECT v.id, v.placa, v.modelo, v.capacidad_toneladas, v.estado, v.fecha_registro,
                       (SELECT COUNT(*) FROM servicios s
                          WHERE s.vehiculo_id = v.id AND s.fecha_fin_real IS NULL) AS servicios_activos
                FROM vehiculos v
                WHERE (@filtro = '' OR v.placa ILIKE '%' || @filtro || '%' OR v.modelo ILIKE '%' || @filtro || '%')
                  AND (@estado = '' OR v.estado = @estado)
                ORDER BY v.id ASC";

            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("filtro", filtro ?? string.Empty);
            cmd.Parameters.AddWithValue("estado", estado ?? string.Empty);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                lista.Add(Mapear(reader));

            return lista;
        }

        public async Task<Vehiculo?> ObtenerPorId(int id)
        {
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            string sql = @"
                SELECT v.id, v.placa, v.modelo, v.capacidad_toneladas, v.estado, v.fecha_registro,
                       (SELECT COUNT(*) FROM servicios s
                          WHERE s.vehiculo_id = v.id AND s.fecha_fin_real IS NULL) AS servicios_activos
                FROM vehiculos v
                WHERE v.id = @id";

            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("id", id);

            using var reader = await cmd.ExecuteReaderAsync();
            return await reader.ReadAsync() ? Mapear(reader) : null;
        }

        // HU-008: validar unicidad de placa (no permitir editar a una placa de otro vehículo)
        public async Task<bool> PlacaEnUsoPorOtro(string placa, int idActual = 0)
        {
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();
            using var cmd = new NpgsqlCommand(
                "SELECT COUNT(*) FROM vehiculos WHERE placa = @p AND id <> @id", conn);
            cmd.Parameters.AddWithValue("p", placa);
            cmd.Parameters.AddWithValue("id", idActual);
            return (long)(await cmd.ExecuteScalarAsync() ?? 0L) > 0;
        }

        // HU-008: cuenta de servicios sin fecha_fin_real
        public async Task<int> ContarServiciosActivos(int vehiculoId)
        {
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();
            using var cmd = new NpgsqlCommand(
                "SELECT COUNT(*) FROM servicios WHERE vehiculo_id = @id AND fecha_fin_real IS NULL", conn);
            cmd.Parameters.AddWithValue("id", vehiculoId);
            return (int)(long)(await cmd.ExecuteScalarAsync() ?? 0L);
        }

        public async Task Registrar(Vehiculo v)
        {
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();
            string sql = @"INSERT INTO vehiculos (placa, modelo, capacidad_toneladas, estado)
                           VALUES (@placa, @modelo, @cap, @estado)";
            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("placa", v.Placa);
            cmd.Parameters.AddWithValue("modelo", v.Modelo);
            cmd.Parameters.AddWithValue("cap", (object?)v.CapacidadToneladas ?? DBNull.Value);
            cmd.Parameters.AddWithValue("estado", string.IsNullOrWhiteSpace(v.Estado) ? "Disponible" : v.Estado);
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task Actualizar(Vehiculo v)
        {
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();
            string sql = @"UPDATE vehiculos
                           SET placa = @placa, modelo = @modelo,
                               capacidad_toneladas = @cap, estado = @estado
                           WHERE id = @id";
            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("placa", v.Placa);
            cmd.Parameters.AddWithValue("modelo", v.Modelo);
            cmd.Parameters.AddWithValue("cap", (object?)v.CapacidadToneladas ?? DBNull.Value);
            cmd.Parameters.AddWithValue("estado", v.Estado);
            cmd.Parameters.AddWithValue("id", v.Id);
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task CambiarEstado(int id, string nuevoEstado)
        {
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();
            using var cmd = new NpgsqlCommand("UPDATE vehiculos SET estado = @e WHERE id = @id", conn);
            cmd.Parameters.AddWithValue("e", nuevoEstado);
            cmd.Parameters.AddWithValue("id", id);
            await cmd.ExecuteNonQueryAsync();
        }

        // Compatibilidad con la firma anterior usada en la vista actual
        public Task ToggleEstado(int id, string nuevoEstado) => CambiarEstado(id, nuevoEstado);

        private static Vehiculo Mapear(NpgsqlDataReader reader) => new Vehiculo
        {
            Id = reader.GetInt32(0),
            Placa = reader.GetString(1),
            Modelo = reader.GetString(2),
            CapacidadToneladas = reader.IsDBNull(3) ? null : reader.GetDecimal(3),
            Estado = reader.IsDBNull(4) ? "Disponible" : reader.GetString(4),
            FechaRegistro = reader.GetDateTime(5),
            ServiciosActivos = (int)(long)reader.GetInt64(6)
        };
    }
}
