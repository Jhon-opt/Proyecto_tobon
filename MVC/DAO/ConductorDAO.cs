using Npgsql;
using TransportesTobonApp.MVC.Models;

namespace TransportesTobonApp.MVC.DAO
{
    public class ConductorDAO
    {
        private readonly string _connectionString;

        public ConductorDAO(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection");
        }

        // HU-010: lista con filtro por nombre/documento y por estado.
        // Teléfono se obtiene de tabla telefonos (uno por conductor, MIN id).
        public async Task<List<Conductor>> Listar(string? filtro = null, string? estado = null)
        {
            var lista = new List<Conductor>();
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            string sql = @"
                SELECT c.id, c.nombre, c.documento, c.licencia, c.estado,
                       (SELECT t.numero FROM telefonos t
                          WHERE t.conductor_id = c.id
                          ORDER BY t.id ASC LIMIT 1) AS telefono,
                       (SELECT COUNT(*) FROM servicios s
                          WHERE s.conductor_id = c.id AND s.fecha_fin_real IS NULL) AS servicios_activos
                FROM conductores c
                WHERE (@filtro = '' OR c.nombre ILIKE '%' || @filtro || '%' OR c.documento ILIKE '%' || @filtro || '%')
                  AND (@estado = '' OR c.estado = @estado)
                ORDER BY c.id ASC";

            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("filtro", filtro ?? string.Empty);
            cmd.Parameters.AddWithValue("estado", estado ?? string.Empty);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                lista.Add(Mapear(reader));

            return lista;
        }

        public async Task<Conductor?> ObtenerPorId(int id)
        {
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            string sql = @"
                SELECT c.id, c.nombre, c.documento, c.licencia, c.estado,
                       (SELECT t.numero FROM telefonos t
                          WHERE t.conductor_id = c.id
                          ORDER BY t.id ASC LIMIT 1) AS telefono,
                       (SELECT COUNT(*) FROM servicios s
                          WHERE s.conductor_id = c.id AND s.fecha_fin_real IS NULL) AS servicios_activos
                FROM conductores c
                WHERE c.id = @id";

            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("id", id);

            using var reader = await cmd.ExecuteReaderAsync();
            return await reader.ReadAsync() ? Mapear(reader) : null;
        }

        // HU-009 / HU-010: validar unicidad del documento
        public async Task<bool> DocumentoEnUsoPorOtro(string documento, int idActual = 0)
        {
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();
            using var cmd = new NpgsqlCommand(
                "SELECT COUNT(*) FROM conductores WHERE documento = @d AND id <> @id", conn);
            cmd.Parameters.AddWithValue("d", documento);
            cmd.Parameters.AddWithValue("id", idActual);
            return (long)(await cmd.ExecuteScalarAsync() ?? 0L) > 0;
        }

        public async Task<int> ContarServiciosActivos(int conductorId)
        {
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();
            using var cmd = new NpgsqlCommand(
                "SELECT COUNT(*) FROM servicios WHERE conductor_id = @id AND fecha_fin_real IS NULL", conn);
            cmd.Parameters.AddWithValue("id", conductorId);
            return (int)(long)(await cmd.ExecuteScalarAsync() ?? 0L);
        }

        // HU-009: registrar conductor + teléfono opcional en una transacción
        public async Task Registrar(Conductor c)
        {
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();
            using var tx = await conn.BeginTransactionAsync();

            string sqlIns = @"INSERT INTO conductores (nombre, documento, licencia, estado)
                              VALUES (@n, @d, @l, @e) RETURNING id";
            int nuevoId;
            using (var cmd = new NpgsqlCommand(sqlIns, conn, tx))
            {
                cmd.Parameters.AddWithValue("n", c.Nombre);
                cmd.Parameters.AddWithValue("d", c.Documento);
                cmd.Parameters.AddWithValue("l", c.Licencia);
                cmd.Parameters.AddWithValue("e", string.IsNullOrWhiteSpace(c.Estado) ? "Disponible" : c.Estado);
                nuevoId = (int)(await cmd.ExecuteScalarAsync() ?? 0);
            }

            if (!string.IsNullOrWhiteSpace(c.Telefono))
            {
                string sqlTel = @"INSERT INTO telefonos (numero, tipo, conductor_id)
                                  VALUES (@n, 'Principal', @cid)";
                using var cmd = new NpgsqlCommand(sqlTel, conn, tx);
                cmd.Parameters.AddWithValue("n", c.Telefono);
                cmd.Parameters.AddWithValue("cid", nuevoId);
                await cmd.ExecuteNonQueryAsync();
            }

            await tx.CommitAsync();
        }

        // HU-010: actualizar (excepto documento, identificador único)
        public async Task Actualizar(Conductor c)
        {
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();
            using var tx = await conn.BeginTransactionAsync();

            string sqlUpd = @"UPDATE conductores
                              SET nombre = @n, licencia = @l, estado = @e
                              WHERE id = @id";
            using (var cmd = new NpgsqlCommand(sqlUpd, conn, tx))
            {
                cmd.Parameters.AddWithValue("n", c.Nombre);
                cmd.Parameters.AddWithValue("l", c.Licencia);
                cmd.Parameters.AddWithValue("e", c.Estado);
                cmd.Parameters.AddWithValue("id", c.Id);
                await cmd.ExecuteNonQueryAsync();
            }

            // Sincronizar teléfono principal
            int? telId;
            using (var cmd = new NpgsqlCommand(
                "SELECT id FROM telefonos WHERE conductor_id = @cid ORDER BY id ASC LIMIT 1", conn, tx))
            {
                cmd.Parameters.AddWithValue("cid", c.Id);
                var r = await cmd.ExecuteScalarAsync();
                telId = r == null || r == DBNull.Value ? (int?)null : (int)r;
            }

            if (string.IsNullOrWhiteSpace(c.Telefono))
            {
                if (telId.HasValue)
                {
                    using var cmd = new NpgsqlCommand("DELETE FROM telefonos WHERE id = @id", conn, tx);
                    cmd.Parameters.AddWithValue("id", telId.Value);
                    await cmd.ExecuteNonQueryAsync();
                }
            }
            else if (telId.HasValue)
            {
                using var cmd = new NpgsqlCommand(
                    "UPDATE telefonos SET numero = @n WHERE id = @id", conn, tx);
                cmd.Parameters.AddWithValue("n", c.Telefono);
                cmd.Parameters.AddWithValue("id", telId.Value);
                await cmd.ExecuteNonQueryAsync();
            }
            else
            {
                using var cmd = new NpgsqlCommand(
                    "INSERT INTO telefonos (numero, tipo, conductor_id) VALUES (@n, 'Principal', @cid)",
                    conn, tx);
                cmd.Parameters.AddWithValue("n", c.Telefono);
                cmd.Parameters.AddWithValue("cid", c.Id);
                await cmd.ExecuteNonQueryAsync();
            }

            await tx.CommitAsync();
        }

        public async Task CambiarEstado(int id, string nuevoEstado)
        {
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();
            using var cmd = new NpgsqlCommand("UPDATE conductores SET estado = @e WHERE id = @id", conn);
            cmd.Parameters.AddWithValue("e", nuevoEstado);
            cmd.Parameters.AddWithValue("id", id);
            await cmd.ExecuteNonQueryAsync();
        }

        // Compatibilidad con la firma anterior
        public Task ToggleEstado(int id, string nuevoEstado) => CambiarEstado(id, nuevoEstado);

        private static Conductor Mapear(NpgsqlDataReader reader) => new Conductor
        {
            Id = reader.GetInt32(0),
            Nombre = reader.GetString(1),
            Documento = reader.GetString(2),
            Licencia = reader.GetString(3),
            Estado = reader.IsDBNull(4) ? "Disponible" : reader.GetString(4),
            Telefono = reader.IsDBNull(5) ? null : reader.GetString(5),
            ServiciosActivos = (int)(long)reader.GetInt64(6)
        };
    }
}
