using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Security.Claims;
using TransportesTobonApp.MVC.Models;
using TransportesTobonApp.MVC.DAO;

namespace TransportesTobonApp.MVC.Controlador
{
    public class AuthController : Controller
    {
        private readonly UsuarioDAO _usuarioDAO;

        public AuthController(UsuarioDAO usuarioDAO)
    {
        _usuarioDAO = usuarioDAO;
    }

        [HttpGet]
        public IActionResult Login() => View();

        [HttpPost]
        public async Task<IActionResult> Login(string email, string password)
        {
            // 1. Criterio HU-002: Bloqueo
            var bloqueo = await _usuarioDAO.VerificarBloqueo(email);
            if (bloqueo.HasValue && bloqueo > DateTime.Now)
            {
                ViewBag.Message = $"Cuenta bloqueada. Intente después de: {bloqueo.Value:HH:mm}";
                return View();
            }

            // 2. Criterio HU-002: Validación
            var usuario = await _usuarioDAO.ValidarLogin(email, password);

            if (usuario != null)
            {
                await _usuarioDAO.ResetearIntentos(email);

                var claims = new List<Claim> {
                    new Claim(ClaimTypes.Name, usuario.Nombre),
                    new Claim(ClaimTypes.Role, usuario.Rol)
                };

                var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                
                // Criterio HU-002: Sesión de 30 min y Recordar Sesión
                var authProperties = new AuthenticationProperties {
                    IsPersistent = true, 
                    ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(30)
                };

                await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, 
                    new ClaimsPrincipal(claimsIdentity), authProperties);

                return RedirectToAction("Index", "Dashboard");
            }

            // 3. Criterio HU-002: Registrar fallo si fallan credenciales
            await _usuarioDAO.RegistrarFallo(email);
            ViewBag.Message = "Credenciales incorrectas o cuenta inactiva.";
            return View();
        }


        [HttpPost]
        [Microsoft.AspNetCore.Authorization.Authorize]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login");
        }

[HttpGet]
public IActionResult Registro() => View();

[HttpPost]
public async Task<IActionResult> Registro(Usuario nuevoUsuario)
{
    try 
    {
        await _usuarioDAO.RegistrarUsuario(nuevoUsuario);
        
     
        TempData["SuccessMessage"] = "Cuenta creada con éxito. Ya puedes ingresar.";
        return RedirectToAction("Login"); 
    } 
    catch (Npgsql.PostgresException ex) when (ex.SqlState == "23505")
    {
        ViewBag.Message = "Este correo electrónico ya está registrado.";
        return View(nuevoUsuario);
    }
    catch (Exception ex)
    {
        ViewBag.Message = "Error en el sistema: " + ex.Message;
        return View(nuevoUsuario);
    }
}
    }
}