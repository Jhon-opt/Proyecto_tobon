using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Npgsql;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Security.Claims;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace TransportesTobonApp.Pages
{
    public class LoginModel : PageModel
    {
        private readonly IConfiguration _configuration;

        public LoginModel(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        [BindProperty]
        public string Email { get; set; }

        [BindProperty]
        public string Password { get; set; }

        public string Message { get; set; }

        // Método principal de Login con seguridad HU-002
        public async Task<IActionResult> OnPostAsync()
        {
            string connectionString = _configuration.GetConnectionString("DefaultConnection");

            using (var conn = new NpgsqlConnection(connectionString))
            {
                await conn.OpenAsync();

                // 1. Verificar si el usuario está bloqueado temporalmente (Criterio: Bloqueo 5 min)
                string checkLockSql = "SELECT intentos_fallidos, bloqueado_hasta FROM \"users\" WHERE \"email\" = @email";
                using (var cmdLock = new NpgsqlCommand(checkLockSql, conn))
                {
                    cmdLock.Parameters.AddWithValue("email", Email);
                    using (var reader = await cmdLock.ExecuteReaderAsync())
                    {
                        if (reader.Read())
                        {
                            var bloqueadoHasta = reader["bloqueado_hasta"] as DateTime?;
                            if (bloqueadoHasta.HasValue && bloqueadoHasta > DateTime.Now)
                            {
                                Message = $"Cuenta bloqueada por seguridad. Intente después de: {bloqueadoHasta.Value:HH:mm}";
                                return Page();
                            }
                        }
                    }
                }

                // 2. Intentar Login con validación de estado activo
                string sql = "SELECT nombre, rol FROM \"users\" WHERE \"email\" = @email AND \"password_hash\" = @pass AND \"estado\" = true";
                using (var cmd = new NpgsqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("email", Email);
                    cmd.Parameters.AddWithValue("pass", Password);

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        if (reader.Read())
                        {
                            // LOGIN EXITOSO: Resetear intentos fallidos
                            await ResetearIntentos(Email, connectionString);

                            // Configurar Identidad del usuario (Claims)
                            var claims = new List<Claim> {
                                new Claim(ClaimTypes.Name, reader["nombre"].ToString()),
                                new Claim(ClaimTypes.Role, reader["rol"].ToString())
                            };

                            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

                            // Configuración de la cookie de sesión (Criterio: Cierre automático 30 min)
                            var authProperties = new AuthenticationProperties
                            {
                                IsPersistent = true, // Permite "Recordar sesión"
                                ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(30)
                            };

                            await HttpContext.SignInAsync(
                                CookieAuthenticationDefaults.AuthenticationScheme,
                                new ClaimsPrincipal(claimsIdentity),
                                authProperties);

                            // Datos para la interfaz inmediata
                            TempData["UsuarioNombre"] = reader["nombre"].ToString();
                            TempData["UsuarioRol"] = reader["rol"].ToString();

                            return RedirectToPage("Dashboard");
                        }
                    }
                }

                // 3. SI FALLA: Registrar el intento fallido (Criterio: Máximo 5 intentos)
                await RegistrarFallo(Email, connectionString);
            }

            Message = "Credenciales incorrectas o cuenta inactiva.";
            return Page();
        }

        // Métodos de apoyo para la seguridad de la HU-002
        private async Task RegistrarFallo(string email, string connString)
        {
            using (var conn = new NpgsqlConnection(connString))
            {
                await conn.OpenAsync();
                // Aumenta el contador y si llega a 5, pone la hora de desbloqueo en 5 minutos
                string sql = @"UPDATE ""users"" SET intentos_fallidos = intentos_fallidos + 1, 
                               bloqueado_hasta = CASE WHEN intentos_fallidos + 1 >= 5 THEN NOW() + INTERVAL '5 minutes' ELSE NULL END 
                               WHERE email = @email";
                using (var cmd = new NpgsqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("email", email);
                    await cmd.ExecuteNonQueryAsync();
                }
            }
        }

        private async Task ResetearIntentos(string email, string connString)
        {
            using (var conn = new NpgsqlConnection(connString))
            {
                await conn.OpenAsync();
                string sql = "UPDATE \"users\" SET intentos_fallidos = 0, bloqueado_hasta = NULL WHERE email = @email";
                using (var cmd = new NpgsqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("email", email);
                    await cmd.ExecuteNonQueryAsync();
                }
            }
        }
    }
}