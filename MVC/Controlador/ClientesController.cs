using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TransportesTobonApp.MVC.DAO;
using TransportesTobonApp.MVC.Models;

namespace TransportesTobonApp.MVC.Controlador
{
    [Authorize(Roles = "Administrador")]
    public class ClientesController : Controller
    {
        private readonly ClienteDAO _clienteDAO;

        public ClientesController(ClienteDAO clienteDAO)
        {
            _clienteDAO = clienteDAO;
        }

        // HU-005: lista con filtro
        public async Task<IActionResult> Index(string? filtro)
        {
            var clientes = await _clienteDAO.Listar(filtro);
            ViewBag.Filtro = filtro;
            return View(clientes);
        }

        // HU-004: registrar
        [HttpGet]
        public IActionResult Crear() => View(new Cliente());

        [HttpPost]
        public async Task<IActionResult> Crear(Cliente cliente)
        {
            if (!ModelState.IsValid) return View(cliente);

            var errorUnicidad = await _clienteDAO.ValidarUnicidad(cliente);
            if (errorUnicidad != null)
            {
                ViewBag.Message = errorUnicidad;
                return View(cliente);
            }

            try
            {
                await _clienteDAO.Registrar(cliente);
                TempData["SuccessMessage"] = "Cliente registrado correctamente.";
                return RedirectToAction("Index");
            }
            catch (Npgsql.PostgresException ex) when (ex.SqlState == "23505")
            {
                ViewBag.Message = "El NIT/CC o email ya está registrado.";
                return View(cliente);
            }
            catch (Exception ex)
            {
                ViewBag.Message = "Error al registrar: " + ex.Message;
                return View(cliente);
            }
        }

        // HU-005: editar (NIT/CC se muestra pero no se modifica)
        [HttpGet]
        public async Task<IActionResult> Editar(int id)
        {
            var cliente = await _clienteDAO.ObtenerPorId(id);
            if (cliente == null) return NotFound();
            return View(cliente);
        }

        [HttpPost]
        public async Task<IActionResult> Editar(Cliente cliente)
        {
            if (!ModelState.IsValid) return View(cliente);

            // Conservamos el NIT/CC original (no editable)
            var actual = await _clienteDAO.ObtenerPorId(cliente.Id);
            if (actual == null) return NotFound();
            cliente.NitCC = actual.NitCC;

            var errorUnicidad = await _clienteDAO.ValidarUnicidad(cliente, cliente.Id);
            if (errorUnicidad != null)
            {
                ViewBag.Message = errorUnicidad;
                return View(cliente);
            }

            try
            {
                await _clienteDAO.Actualizar(cliente);
                TempData["SuccessMessage"] = "Cliente actualizado correctamente.";
                return RedirectToAction("Index");
            }
            catch (Npgsql.PostgresException ex) when (ex.SqlState == "23505")
            {
                ViewBag.Message = "El email ya está registrado para otro cliente.";
                return View(cliente);
            }
            catch (Exception ex)
            {
                ViewBag.Message = "Error al actualizar: " + ex.Message;
                return View(cliente);
            }
        }

        // HU-005 / CdP_006: activar/desactivar.
        // Al desactivar, el historial de servicios se conserva (sólo se inhabilita para nuevos pedidos).
        [HttpPost]
        public async Task<IActionResult> ToggleEstado(int id, bool estadoActual)
        {
            await _clienteDAO.ToggleEstado(id, !estadoActual);
            TempData["SuccessMessage"] = !estadoActual
                ? "Cliente activado correctamente."
                : "Cliente desactivado. Su historial de servicios se conserva, pero no podrá ser usado en nuevos pedidos.";
            return RedirectToAction("Index");
        }
    }
}
