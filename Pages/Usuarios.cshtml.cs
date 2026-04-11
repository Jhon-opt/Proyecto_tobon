using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Npgsql;
using System.Collections.Generic;

namespace TransportesTobonApp.Pages
{
    public class UsuariosModel : PageModel
    {
        private readonly IConfiguration _configuration;
        public List<UsuarioInfo> ListaUsuarios = new List<UsuarioInfo>();

        public UsuariosModel(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        // Cambiamos a IActionResult para poder redireccionar si no es Admin
        public IActionResult OnGet()
        {
            // SEGURIDAD: Usamos Peek para leer el rol sin que se borre del TempData
            var rolLogueado = TempData.Peek("UsuarioRol")?.ToString();

            if (rolLogueado != "Administrador")
            {
                // Si no es admin, lo expulsamos al Dashboard
                return RedirectToPage("Dashboard");
            }

            CargarUsuarios();
            return Page();
        }

        private void CargarUsuarios()
        {
            string connString = _configuration.GetConnectionString("DefaultConnection");
            using (var conn = new NpgsqlConnection(connString))
            {
                conn.Open();
                // Traemos todos los usuarios para cumplir la HU-003
                string sql = "SELECT id, nombre, email, rol, estado FROM \"users\" ORDER BY id ASC";
                using (var cmd = new NpgsqlCommand(sql, conn))
                {
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            ListaUsuarios.Add(new UsuarioInfo {
                                Id = reader.GetInt32(0),
                                Nombre = reader.GetString(1),
                                Email = reader.GetString(2),
                                Rol = reader.GetString(3),
                                Estado = reader.GetBoolean(4)
                            });
                        }
                    }
                }
            }
        }

        // MÉTODO PARA DESACTIVAR/ACTIVAR (Eliminación lógica de la HU-003)
        public IActionResult OnPostToggleEstado(int id, bool estadoActual)
        {
            string connString = _configuration.GetConnectionString("DefaultConnection");
            using (var conn = new NpgsqlConnection(connString))
            {
                conn.Open();
                // Cambiamos el estado al valor opuesto
                string sql = "UPDATE \"users\" SET \"estado\" = @nuevoEstado WHERE \"id\" = @id";
                using (var cmd = new NpgsqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("nuevoEstado", !estadoActual);
                    cmd.Parameters.AddWithValue("id", id);
                    cmd.ExecuteNonQuery();
                }
            }
            // Refresca la página para ver los cambios
            return RedirectToPage();
        }

        // MÉTODO PARA ELIMINAR FÍSICAMENTE (HU-003)
public IActionResult OnPostEliminar(int id)
{
    string connString = _configuration.GetConnectionString("DefaultConnection");
    using (var conn = new NpgsqlConnection(connString))
    {
        conn.Open();
        // SQL para borrar permanentemente
        string sql = "DELETE FROM \"users\" WHERE \"id\" = @id";
        using (var cmd = new NpgsqlCommand(sql, conn))
        {
            cmd.Parameters.AddWithValue("id", id);
            cmd.ExecuteNonQuery();
        }
    }
    // Recargamos la lista
    return RedirectToPage();
}
    }

    public class UsuarioInfo {
        public int Id { get; set; }
        public string Nombre { get; set; }
        public string Email { get; set; }
        public string Rol { get; set; }
        public bool Estado { get; set; }
    }

    
}