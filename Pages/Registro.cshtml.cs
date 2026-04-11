using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Npgsql;

namespace TransportesTobonApp.Pages
{
    public class RegistroModel : PageModel
    {
        private readonly IConfiguration _configuration;

        public RegistroModel(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        // ¡IMPORTANTE! Esto es lo que conecta el HTML con el C#
        [BindProperty]
        public string Nombre { get; set; }
        [BindProperty]
        public string Email { get; set; }
        [BindProperty]
        public string Password { get; set; }

        [BindProperty]
        public string Rol { get; set; }
        
        public string Message { get; set; }

        public void OnGet() { }

        public IActionResult OnPost()
        {
            // Tu lógica estaba perfecta, solo asegúrate de que el puerto sea 5433 en appsettings
            string connString = _configuration.GetConnectionString("DefaultConnection");
            
            using (var conn = new NpgsqlConnection(connString))
            {
                conn.Open();
                // Usamos "users" entre comillas por tu esquema de Postgres
                string sql = "INSERT INTO \"users\" (nombre, email, password_hash, rol, estado) VALUES (@n, @e, @p, @r, true)";
                
                using (var cmd = new NpgsqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("n", Nombre);
                    cmd.Parameters.AddWithValue("e", Email);
                    cmd.Parameters.AddWithValue("p", Password);
                    cmd.Parameters.AddWithValue("r", Rol);

                    try 
                    {
                        cmd.ExecuteNonQuery();
                        TempData["UsuarioNombre"] = Nombre;
                        return RedirectToPage("Dashboard"); 
                    } 
                    catch (PostgresException ex) when (ex.SqlState == "23505") // Error de duplicado en Postgres
                    {
                        Message = "Este correo electrónico ya está registrado.";
                        return Page();
                    }
                    catch (Exception ex)
                    {
                        Message = "Ocurrió un error: " + ex.Message;
                        return Page();
                    }
                }
            }
        }
    }
}