using Npgsql;
using TransportesTobonApp.MVC.Models;

namespace TransportesTobonApp.MVC.DAO
{
    public class ClienteDAO
    {
        private readonly string _connectionString;

        public ClienteDAO(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection");
        }

        // HU-005: lista con filtro por nombre o NIT/CC
        // El teléfono se obtiene de la tabla telefonos (uno por cliente, MIN(id)).
        public async Task<List<Cliente>> Listar(string? filtro = null)
        {
            var lista = new List<Cliente>();
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            string sql = @"
                SELECT c.id, c.nombre_razon_social, c.nit_cc, c.email, c.direccion,
                       c.notas, c.estado, c.fecha_creacion,
                       (SELECT t.numero FROM telefonos t
                         WHERE t.cliente_id = c.id
                         ORDER BY t.id ASC LIMIT 1) AS telefono
                FROM clientes c
                WHERE (@filtro = '' OR c.nombre_razon_social ILIKE '%' || @filtro || '%' OR c.nit_cc ILIKE '%' || @filtro || '%')
                ORDER BY c.id ASC";

            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("filtro", filtro ?? string.Empty);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                lista.Add(MapearCliente(reader));
            }
            return lista;
        }

        public async Task<Cliente?> ObtenerPorId(int id)
        {
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            string sql = @"
                SELECT c.id, c.nombre_razon_social, c.nit_cc, c.email, c.direccion,
                       c.notas, c.estado, c.fecha_creacion,
                       (SELECT t.numero FROM telefonos t
                         WHERE t.cliente_id = c.id
                         ORDER BY t.id ASC LIMIT 1) AS telefono
                FROM clientes c
                WHERE c.id = @id";
            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("id", id);

            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return MapearCliente(reader);
            }
            return null;
        }

        // HU-004 / HU-005: validar unicidad de NIT/CC y email contra otros clientes
        public async Task<string?> ValidarUnicidad(Cliente cliente, int idActual = 0)
        {
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            string sqlNit = "SELECT COUNT(*) FROM clientes WHERE nit_cc = @v AND id <> @id";
            using (var cmd = new NpgsqlCommand(sqlNit, conn))
            {
                cmd.Parameters.AddWithValue("v", cliente.NitCC);
                cmd.Parameters.AddWithValue("id", idActual);
                if ((long)(await cmd.ExecuteScalarAsync() ?? 0L) > 0)
                    return "El NIT/CC ya está registrado para otro cliente.";
            }

            if (!string.IsNullOrWhiteSpace(cliente.Email))
            {
                string sqlEmail = "SELECT COUNT(*) FROM clientes WHERE email = @v AND id <> @id";
                using var cmd = new NpgsqlCommand(sqlEmail, conn);
                cmd.Parameters.AddWithValue("v", cliente.Email);
                cmd.Parameters.AddWithValue("id", idActual);
                if ((long)(await cmd.ExecuteScalarAsync() ?? 0L) > 0)
                    return "El email ya está registrado para otro cliente.";
            }

            // HU-005: validar que el teléfono no esté en uso por otro cliente
            if (!string.IsNullOrWhiteSpace(cliente.Telefono))
            {
                string sqlTel = @"SELECT COUNT(*) FROM telefonos
                                  WHERE numero = @v
                                    AND cliente_id IS NOT NULL
                                    AND cliente_id <> @id";
                using var cmd = new NpgsqlCommand(sqlTel, conn);
                cmd.Parameters.AddWithValue("v", cliente.Telefono);
                cmd.Parameters.AddWithValue("id", idActual);
                if ((long)(await cmd.ExecuteScalarAsync() ?? 0L) > 0)
                    return "El teléfono ya está registrado para otro cliente.";
            }

            return null;
        }

        // HU-004: registrar cliente. El teléfono se guarda como un registro
        // en la tabla telefonos (tipo='Principal'). Se usa una transacción
        // para mantener cliente + teléfono consistentes.
        public async Task Registrar(Cliente cliente)
        {
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();
            using var tx = await conn.BeginTransactionAsync();

            string sqlCliente = @"
                INSERT INTO clientes (nombre_razon_social, nit_cc, email, direccion, notas, estado)
                VALUES (@nombre, @nit, @email, @dir, @nota, true)
                RETURNING id";

            int nuevoId;
            using (var cmd = new NpgsqlCommand(sqlCliente, conn, tx))
            {
                cmd.Parameters.AddWithValue("nombre", cliente.Nombre);
                cmd.Parameters.AddWithValue("nit", cliente.NitCC);
                cmd.Parameters.AddWithValue("email", (object?)cliente.Email ?? DBNull.Value);
                cmd.Parameters.AddWithValue("dir", (object?)cliente.Direccion ?? DBNull.Value);
                cmd.Parameters.AddWithValue("nota", (object?)cliente.Nota ?? DBNull.Value);
                nuevoId = (int)(await cmd.ExecuteScalarAsync() ?? 0);
            }

            if (!string.IsNullOrWhiteSpace(cliente.Telefono))
            {
                string sqlTel = @"INSERT INTO telefonos (numero, tipo, cliente_id)
                                  VALUES (@numero, 'Principal', @cid)";
                using var cmd = new NpgsqlCommand(sqlTel, conn, tx);
                cmd.Parameters.AddWithValue("numero", cliente.Telefono);
                cmd.Parameters.AddWithValue("cid", nuevoId);
                await cmd.ExecuteNonQueryAsync();
            }

            await tx.CommitAsync();
        }

        // HU-005: actualizar todos los campos excepto NIT/CC.
        // El teléfono "principal" se sincroniza en la tabla telefonos.
        public async Task Actualizar(Cliente cliente)
        {
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();
            using var tx = await conn.BeginTransactionAsync();

            string sqlCliente = @"
                UPDATE clientes
                SET nombre_razon_social = @nombre,
                    email = @email,
                    direccion = @dir,
                    notas = @nota
                WHERE id = @id";

            using (var cmd = new NpgsqlCommand(sqlCliente, conn, tx))
            {
                cmd.Parameters.AddWithValue("nombre", cliente.Nombre);
                cmd.Parameters.AddWithValue("email", (object?)cliente.Email ?? DBNull.Value);
                cmd.Parameters.AddWithValue("dir", (object?)cliente.Direccion ?? DBNull.Value);
                cmd.Parameters.AddWithValue("nota", (object?)cliente.Nota ?? DBNull.Value);
                cmd.Parameters.AddWithValue("id", cliente.Id);
                await cmd.ExecuteNonQueryAsync();
            }

            // Sincronizar teléfono principal
            int? telId;
            using (var cmd = new NpgsqlCommand(
                "SELECT id FROM telefonos WHERE cliente_id = @cid ORDER BY id ASC LIMIT 1", conn, tx))
            {
                cmd.Parameters.AddWithValue("cid", cliente.Id);
                var r = await cmd.ExecuteScalarAsync();
                telId = r == null || r == DBNull.Value ? (int?)null : (int)r;
            }

            if (string.IsNullOrWhiteSpace(cliente.Telefono))
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
                    "UPDATE telefonos SET numero = @numero WHERE id = @id", conn, tx);
                cmd.Parameters.AddWithValue("numero", cliente.Telefono);
                cmd.Parameters.AddWithValue("id", telId.Value);
                await cmd.ExecuteNonQueryAsync();
            }
            else
            {
                using var cmd = new NpgsqlCommand(
                    "INSERT INTO telefonos (numero, tipo, cliente_id) VALUES (@numero, 'Principal', @cid)",
                    conn, tx);
                cmd.Parameters.AddWithValue("numero", cliente.Telefono);
                cmd.Parameters.AddWithValue("cid", cliente.Id);
                await cmd.ExecuteNonQueryAsync();
            }

            await tx.CommitAsync();
        }

        // HU-005: cambiar estado (activar/desactivar)
        public async Task ToggleEstado(int id, bool nuevoEstado)
        {
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();
            string sql = "UPDATE clientes SET estado = @est WHERE id = @id";
            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("est", nuevoEstado);
            cmd.Parameters.AddWithValue("id", id);
            await cmd.ExecuteNonQueryAsync();
        }

        private static Cliente MapearCliente(NpgsqlDataReader reader)
        {
            return new Cliente
            {
                Id = reader.GetInt32(0),
                Nombre = reader.GetString(1),
                NitCC = reader.GetString(2),
                Email = reader.IsDBNull(3) ? null : reader.GetString(3),
                Direccion = reader.IsDBNull(4) ? null : reader.GetString(4),
                Nota = reader.IsDBNull(5) ? null : reader.GetString(5),
                Estado = reader.GetBoolean(6),
                FechaCreacion = reader.GetDateTime(7),
                Telefono = reader.IsDBNull(8) ? null : reader.GetString(8)
            };
        }
    }
}
