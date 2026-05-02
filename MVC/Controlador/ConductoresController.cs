using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TransportesTobonApp.MVC.DAO;
using TransportesTobonApp.MVC.Models;

namespace TransportesTobonApp.MVC.Controlador
{
    [Authorize(Roles = "Administrador")]
    public class ConductoresController : Controller
    {
        private readonly ConductorDAO _dao;

        public ConductoresController(ConductorDAO dao)
        {
            _dao = dao;
        }

        // HU-010: lista con filtro por nombre/documento y por estado
        public async Task<IActionResult> Index(string? filtro, string? estado)
        {
            var lista = await _dao.Listar(filtro, estado);
            ViewBag.Filtro = filtro;
            ViewBag.Estado = estado;
            return View(lista);
        }

        // HU-009: registrar
        [HttpGet]
        public IActionResult Crear() => View(new Conductor());

        [HttpPost]
        public async Task<IActionResult> Crear(Conductor conductor)
        {
            if (!ModelState.IsValid) return View(conductor);

            if (await _dao.DocumentoEnUsoPorOtro(conductor.Documento))
            {
                ViewBag.Message = "El documento ya está registrado para otro conductor.";
                return View(conductor);
            }

            try
            {
                if (string.IsNullOrWhiteSpace(conductor.Estado))
                    conductor.Estado = "Disponible";
                await _dao.Registrar(conductor);
                TempData["SuccessMessage"] = "Conductor registrado correctamente.";
                return RedirectToAction("Index");
            }
            catch (Npgsql.PostgresException ex) when (ex.SqlState == "23505")
            {
                ViewBag.Message = "El documento ya está registrado.";
                return View(conductor);
            }
            catch (Exception ex)
            {
                ViewBag.Message = "Error al registrar: " + ex.Message;
                return View(conductor);
            }
        }

        // HU-010: editar (no permite cambiar el documento)
        [HttpGet]
        public async Task<IActionResult> Editar(int id)
        {
            var c = await _dao.ObtenerPorId(id);
            if (c == null) return NotFound();
            return View(c);
        }

        [HttpPost]
        public async Task<IActionResult> Editar(Conductor conductor, bool confirmado = false)
        {
            if (!ModelState.IsValid) return View(conductor);

            // Conservamos el documento original (HU-010: no editable como identificador único)
            var actual = await _dao.ObtenerPorId(conductor.Id);
            if (actual == null) return NotFound();
            conductor.Documento = actual.Documento;

            if (Conductor.EstadosNoDisponibles.Contains(conductor.Estado))
            {
                int activos = await _dao.ContarServiciosActivos(conductor.Id);
                if (activos > 0 && !confirmado)
                {
                    ViewBag.RequiereConfirmacion = true;
                    ViewBag.Message = $"Este conductor tiene {activos} servicio(s) activo(s). Confirme para continuar.";
                    conductor.ServiciosActivos = activos;
                    return View(conductor);
                }
            }

            try
            {
                await _dao.Actualizar(conductor);
                TempData["SuccessMessage"] = "Conductor actualizado correctamente.";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                ViewBag.Message = "Error al actualizar: " + ex.Message;
                return View(conductor);
            }
        }

        // HU-010: cambio rápido de estado desde el listado
        [HttpPost]
        public async Task<IActionResult> CambiarEstado(int id, string nuevoEstado, bool confirmado = false)
        {
            if (!Conductor.EstadosValidos.Contains(nuevoEstado))
            {
                TempData["SuccessMessage"] = "Estado inválido.";
                return RedirectToAction("Index");
            }

            if (Conductor.EstadosNoDisponibles.Contains(nuevoEstado))
            {
                int activos = await _dao.ContarServiciosActivos(id);
                if (activos > 0 && !confirmado)
                {
                    TempData["WarningMessage"] = $"El conductor tiene {activos} servicio(s) activo(s). Cambio no aplicado.";
                    return RedirectToAction("Index");
                }
            }

            await _dao.CambiarEstado(id, nuevoEstado);
            TempData["SuccessMessage"] = $"Conductor marcado como {nuevoEstado}.";
            return RedirectToAction("Index");
        }
    }
}
