using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TransportesTobonApp.MVC.DAO;
using TransportesTobonApp.MVC.Models;

namespace TransportesTobonApp.MVC.Controlador
{
    [Authorize(Roles = "Administrador")]
    public class UsuariosController : Controller
    {
        private readonly UsuarioDAO _usuarioDAO;

        public UsuariosController(UsuarioDAO usuarioDAO)
        {
            _usuarioDAO = usuarioDAO;
        }

        // HU-003: lista con filtro por nombre o email
        public async Task<IActionResult> Index(string? filtro)
        {
            var usuarios = await _usuarioDAO.ListarUsuarios(filtro);
            ViewBag.Filtro = filtro;
            return View(usuarios);
        }

        [HttpPost]
        public async Task<IActionResult> ToggleEstado(int id, bool estadoActual)
        {
            await _usuarioDAO.ToggleEstado(id, !estadoActual);
            TempData["SuccessMessage"] = !estadoActual
                ? "Usuario activado correctamente."
                : "Usuario desactivado. Ya no podrá iniciar sesión.";
            return RedirectToAction("Index");
        }

        [HttpPost]
        public async Task<IActionResult> Eliminar(int id)
        {
            await _usuarioDAO.Eliminar(id);
            TempData["SuccessMessage"] = "Usuario eliminado correctamente.";
            return RedirectToAction("Index");
        }

        [HttpGet]
        public async Task<IActionResult> Editar(int id)
        {
            var usuario = await _usuarioDAO.ObtenerPorId(id);
            if (usuario == null) return NotFound();
            return View(usuario);
        }

        [HttpPost]
        public async Task<IActionResult> Editar(Usuario usuarioActualizado)
        {
            if (!ModelState.IsValid) return View(usuarioActualizado);

            // HU-003: validación de unicidad de email
            if (await _usuarioDAO.EmailEnUsoPorOtro(usuarioActualizado.Email, usuarioActualizado.Id))
            {
                ViewBag.Message = "El correo ya está asociado a otro usuario.";
                return View(usuarioActualizado);
            }

            try
            {
                await _usuarioDAO.ActualizarUsuario(usuarioActualizado);
                TempData["SuccessMessage"] = "Usuario actualizado correctamente.";
                return RedirectToAction("Index");
            }
            catch (Npgsql.PostgresException ex) when (ex.SqlState == "23505")
            {
                ViewBag.Message = "El correo ya está asociado a otro usuario.";
                return View(usuarioActualizado);
            }
            catch (Exception ex)
            {
                ViewBag.Message = "Error al actualizar: " + ex.Message;
                return View(usuarioActualizado);
            }
        }
    }
}
