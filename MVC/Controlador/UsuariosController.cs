using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TransportesTobonApp.MVC.DAO;
using TransportesTobonApp.MVC.Models;

namespace TransportesTobonApp.MVC.Controlador
{
    [Authorize(Roles = "Administrador")] // Seguridad nivel Dios
    public class UsuariosController : Controller
    {
        private readonly UsuarioDAO _usuarioDAO;

        public UsuariosController(UsuarioDAO usuarioDAO)
        {
            _usuarioDAO = usuarioDAO;
        }

        public async Task<IActionResult> Index()
        {
            var usuarios = await _usuarioDAO.ListarUsuarios();
            return View(usuarios);
        }

        [HttpPost]
        public async Task<IActionResult> ToggleEstado(int id, bool estadoActual)
        {
            await _usuarioDAO.ToggleEstado(id, !estadoActual);
            return RedirectToAction("Index");
        }

        [HttpPost]
        public async Task<IActionResult> Eliminar(int id)
        {
            await _usuarioDAO.Eliminar(id);
            return RedirectToAction("Index");
        }

        // GET: Usuarios/Editar/5
[HttpGet]
public async Task<IActionResult> Editar(int id)
{
    var usuario = await _usuarioDAO.ObtenerPorId(id); // El método que ya definimos antes
    if (usuario == null) return NotFound();
    
    return View(usuario);
}

// POST: Usuarios/Editar
[HttpPost]
public async Task<IActionResult> Editar(Usuario usuarioActualizado)
{
    // Validamos que el modelo sea correcto según las data annotations (si las usas)
    if (!ModelState.IsValid) return View(usuarioActualizado);

    try 
    {
        await _usuarioDAO.ActualizarUsuario(usuarioActualizado);
        return RedirectToAction("Index");
    }
    catch (Exception ex)
    {
        ViewBag.Message = "Error al actualizar: " + ex.Message;
        return View(usuarioActualizado);
    }
}
    }
}