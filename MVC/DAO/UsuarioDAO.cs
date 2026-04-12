using Npgsql;
using TransportesTobonApp.MVC.Models;

namespace TransportesTobonApp.MVC.DAO
{
    public class UsuarioDAO
    {
        private readonly string _connectionString;

        public UsuarioDAO(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection");
        }

        // HU-002: Verificar bloqueo de 5 minutos
        public async Task<DateTime?> VerificarBloqueo(string email)
        {
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();
            string sql = "SELECT bloqueado_hasta FROM \"users\" WHERE \"email\" = @email";
            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("email", email);
            return await cmd.ExecuteScalarAsync() as DateTime?;
        }

        // HU-002: Validar credenciales
        public async Task<Usuario> ValidarLogin(string email, string password)
        {
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();
            string sql = "SELECT nombre, rol FROM \"users\" WHERE \"email\" = @email AND \"password_hash\" = @pass AND \"estado\" = true";
            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("email", email);
            cmd.Parameters.AddWithValue("pass", password);

            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return new Usuario {
                    Nombre = reader["nombre"].ToString(),
                    Rol = reader["rol"].ToString(),
                    Email = email
                };
            }
            return null;
        }

        public async Task RegistrarFallo(string email)
        {
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();
            string sql = @"UPDATE ""users"" SET intentos_fallidos = intentos_fallidos + 1, 
                           bloqueado_hasta = CASE WHEN intentos_fallidos + 1 >= 5 THEN NOW() + INTERVAL '5 minutes' ELSE NULL END 
                           WHERE email = @email";
            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("email", email);
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task ResetearIntentos(string email)
        {
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();
            string sql = "UPDATE \"users\" SET intentos_fallidos = 0, bloqueado_hasta = NULL WHERE email = @email";
            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("email", email);
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task<bool> RegistrarUsuario(Usuario usuario)
{
    using var conn = new NpgsqlConnection(_connectionString);
    await conn.OpenAsync();
    
    // Usamos "users" entre comillas por el esquema de tu BD
    string sql = "INSERT INTO \"users\" (nombre, email, password_hash, rol, estado) VALUES (@n, @e, @p, @r, true)";
    
    using var cmd = new NpgsqlCommand(sql, conn);
    cmd.Parameters.AddWithValue("n", usuario.Nombre);
    cmd.Parameters.AddWithValue("e", usuario.Email);
    cmd.Parameters.AddWithValue("p", usuario.Password);
    cmd.Parameters.AddWithValue("r", usuario.Rol);

    await cmd.ExecuteNonQueryAsync();
    return true;
}

    public async Task<List<Usuario>> ListarUsuarios()
{
    var lista = new List<Usuario>();
    using var conn = new NpgsqlConnection(_connectionString);
    await conn.OpenAsync();
    
    string sql = "SELECT id, nombre, email, rol, estado FROM \"users\" ORDER BY id ASC";
    using var cmd = new NpgsqlCommand(sql, conn);
    using var reader = await cmd.ExecuteReaderAsync();

    while (await reader.ReadAsync())
    {
        lista.Add(new Usuario {
            Id = reader.GetInt32(0),
            Nombre = reader.GetString(1),
            Email = reader.GetString(2),
            Rol = reader.GetString(3),
            Estado = reader.GetBoolean(4)
        });
    }
    return lista;
}

public async Task ToggleEstado(int id, bool nuevoEstado)
{
    using var conn = new NpgsqlConnection(_connectionString);
    await conn.OpenAsync();
    string sql = "UPDATE \"users\" SET estado = @est WHERE id = @id";
    using var cmd = new NpgsqlCommand(sql, conn);
    cmd.Parameters.AddWithValue("est", nuevoEstado);
    cmd.Parameters.AddWithValue("id", id);
    await cmd.ExecuteNonQueryAsync();
}

public async Task Eliminar(int id)
{
    using var conn = new NpgsqlConnection(_connectionString);
    await conn.OpenAsync();
    string sql = "DELETE FROM \"users\" WHERE id = @id";
    using var cmd = new NpgsqlCommand(sql, conn);
    cmd.Parameters.AddWithValue("id", id);
    await cmd.ExecuteNonQueryAsync();
}

public async Task ActualizarUsuario(Usuario usuario)
{
    using var conn = new NpgsqlConnection(_connectionString);
    await conn.OpenAsync();
    
    // Actualizamos los datos básicos. 
    // Nota: Por seguridad, la contraseña suele manejarse en un proceso aparte.
    string sql = "UPDATE \"users\" SET \"nombre\"=@n, \"email\"=@e, \"rol\"=@r WHERE \"id\"=@id";
    
    using var cmd = new NpgsqlCommand(sql, conn);
    cmd.Parameters.AddWithValue("n", usuario.Nombre);
    cmd.Parameters.AddWithValue("e", usuario.Email);
    cmd.Parameters.AddWithValue("r", usuario.Rol);
    cmd.Parameters.AddWithValue("id", usuario.Id);

    await cmd.ExecuteNonQueryAsync();
}

public async Task<Usuario> ObtenerPorId(int id)
{
    using var conn = new NpgsqlConnection(_connectionString);
    await conn.OpenAsync();
    
    // Buscamos solo el registro que coincida con el ID
    string sql = "SELECT id, nombre, email, rol, estado FROM \"users\" WHERE id = @id";
    
    using var cmd = new NpgsqlCommand(sql, conn);
    cmd.Parameters.AddWithValue("id", id);
    
    using var reader = await cmd.ExecuteReaderAsync();
    
    if (await reader.ReadAsync())
    {
        return new Usuario {
            Id = reader.GetInt32(0),
            Nombre = reader.GetString(1),
            Email = reader.GetString(2),
            Rol = reader.GetString(3),
            Estado = reader.GetBoolean(4)
        };
    }
    
    return null; // Si no lo encuentra (poco probable si viene de la lista)
}
    }
}