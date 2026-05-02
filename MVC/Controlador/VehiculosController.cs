using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TransportesTobonApp.MVC.DAO;
using TransportesTobonApp.MVC.Models;

namespace TransportesTobonApp.MVC.Controlador
{
    [Authorize(Roles = "Administrador")]
    public class VehiculosController : Controller
    {
        private readonly VehiculoDAO _dao;

        public VehiculosController(VehiculoDAO dao)
        {
            _dao = dao;
        }

        // HU-008: lista con filtro por placa/modelo y por estado
        public async Task<IActionResult> Index(string? filtro, string? estado)
        {
            var lista = await _dao.Listar(filtro, estado);
            ViewBag.Filtro = filtro;
            ViewBag.Estado = estado;
            return View(lista);
        }

        [HttpGet]
        public IActionResult Crear() => View(new Vehiculo());

        [HttpPost]
        public async Task<IActionResult> Crear(Vehiculo vehiculo)
        {
            // CdP_007: estado inicial siempre Disponible al crear
            vehiculo.Estado = "Disponible";

            if (!ModelState.IsValid) return View(vehiculo);

            if (await _dao.PlacaEnUsoPorOtro(vehiculo.Placa))
            {
                ViewBag.Message = "La placa ya está registrada para otro vehículo.";
                return View(vehiculo);
            }

            try
            {
                await _dao.Registrar(vehiculo);
                TempData["SuccessMessage"] = "Vehículo registrado correctamente.";
                return RedirectToAction("Index");
            }
            catch (Npgsql.PostgresException ex) when (ex.SqlState == "23505")
            {
                ViewBag.Message = "La placa ya está registrada.";
                return View(vehiculo);
            }
            catch (Exception ex)
            {
                ViewBag.Message = "Error al registrar: " + ex.Message;
                return View(vehiculo);
            }
        }

        // HU-008: editar
        [HttpGet]
        public async Task<IActionResult> Editar(int id)
        {
            var v = await _dao.ObtenerPorId(id);
            if (v == null) return NotFound();
            return View(v);
        }

        [HttpPost]
        public async Task<IActionResult> Editar(Vehiculo vehiculo, bool confirmado = false)
        {
            if (!ModelState.IsValid) return View(vehiculo);

            // CdP_007: la placa NO se puede modificar tras el registro.
            // Conservamos siempre la placa original almacenada en BD.
            var actual = await _dao.ObtenerPorId(vehiculo.Id);
            if (actual == null) return NotFound();
            vehiculo.Placa = actual.Placa;

            // HU-008: si va a estado no-disponible y tiene servicios activos, exigir confirmación
            if (Vehiculo.EstadosNoDisponibles.Contains(vehiculo.Estado))
            {
                int activos = await _dao.ContarServiciosActivos(vehiculo.Id);
                if (activos > 0 && !confirmado)
                {
                    ViewBag.RequiereConfirmacion = true;
                    ViewBag.Message = $"Este vehículo tiene {activos} servicio(s) activo(s). Confirme para continuar.";
                    vehiculo.ServiciosActivos = activos;
                    return View(vehiculo);
                }
            }

            try
            {
                await _dao.Actualizar(vehiculo);
                TempData["SuccessMessage"] = "Vehículo actualizado correctamente.";
                return RedirectToAction("Index");
            }
            catch (Npgsql.PostgresException ex) when (ex.SqlState == "23505")
            {
                ViewBag.Message = "La placa ya está registrada para otro vehículo.";
                return View(vehiculo);
            }
            catch (Exception ex)
            {
                ViewBag.Message = "Error al actualizar: " + ex.Message;
                return View(vehiculo);
            }
        }

        // HU-008: cambio rápido de estado desde el listado
        // 'confirmado=true' permite forzar el cambio a no-disponible aunque haya servicios activos
        [HttpPost]
        public async Task<IActionResult> CambiarEstado(int id, string nuevoEstado, bool confirmado = false)
        {
            if (!Vehiculo.EstadosValidos.Contains(nuevoEstado))
            {
                TempData["SuccessMessage"] = "Estado inválido.";
                return RedirectToAction("Index");
            }

            if (Vehiculo.EstadosNoDisponibles.Contains(nuevoEstado))
            {
                int activos = await _dao.ContarServiciosActivos(id);
                if (activos > 0 && !confirmado)
                {
                    TempData["WarningMessage"] = $"El vehículo tiene {activos} servicio(s) activo(s). Cambio no aplicado.";
                    return RedirectToAction("Index");
                }
            }

            await _dao.CambiarEstado(id, nuevoEstado);
            TempData["SuccessMessage"] = $"Vehículo marcado como {nuevoEstado}.";
            return RedirectToAction("Index");
        }
    }
}
