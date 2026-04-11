using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Npgsql;

namespace TransportesTobonApp.Pages
{
    public class EditarUsuarioModel : PageModel
    {
        private readonly IConfiguration _configuration;
        public EditarUsuarioModel(IConfiguration configuration) => _configuration = configuration;

        [BindProperty] public int Id { get; set; }
        [BindProperty] public string Nombre { get; set; }
        [BindProperty] public string Email { get; set; }
        [BindProperty] public string Rol { get; set; }

        public IActionResult OnGet(int id)
        {
            // Seguridad: Solo admin edita
            if (TempData.Peek("UsuarioRol")?.ToString() != "Administrador") return RedirectToPage("Dashboard");

            string connString = _configuration.GetConnectionString("DefaultConnection");
            using (var conn = new NpgsqlConnection(connString))
            {
                conn.Open();
                string sql = "SELECT id, nombre, email, rol FROM \"users\" WHERE id = @id";
                using (var cmd = new NpgsqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("id", id);
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            Id = reader.GetInt32(0);
                            Nombre = reader.GetString(1);
                            Email = reader.GetString(2);
                            Rol = reader.GetString(3);
                        }
                    }
                }
            }
            return Page();
        }

        public IActionResult OnPost()
        {
            string connString = _configuration.GetConnectionString("DefaultConnection");
            using (var conn = new NpgsqlConnection(connString))
            {
                conn.Open();
                string sql = "UPDATE \"users\" SET nombre=@n, email=@e, rol=@r WHERE id=@id";
                using (var cmd = new NpgsqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("n", Nombre);
                    cmd.Parameters.AddWithValue("e", Email);
                    cmd.Parameters.AddWithValue("r", Rol);
                    cmd.Parameters.AddWithValue("id", Id);
                    cmd.ExecuteNonQuery();
                }
            }
            return RedirectToPage("Usuarios");
        }
    }
}