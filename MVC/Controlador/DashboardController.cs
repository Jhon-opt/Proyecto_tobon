using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace TransportesTobonApp.MVC.Controlador
{
    [Authorize] // Protege la ruta: solo usuarios autenticados
    public class DashboardController : Controller
    {
        public IActionResult Index()
        {
            // El nombre y rol ya vienen en la Cookie de sesión
            return View();
        }
    }
}