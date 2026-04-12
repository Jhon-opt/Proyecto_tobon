using Microsoft.AspNetCore.Mvc;

namespace TransportesTobonApp.MVC.Controlador
{
    public class HomeController : Controller
    {
        // Este método responde a la ruta "/"
        public IActionResult Index()
        {
            return View();
        }
    }
}