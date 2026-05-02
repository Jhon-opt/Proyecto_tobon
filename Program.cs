using Microsoft.AspNetCore.Authentication.Cookies;
using TransportesTobonApp.MVC.DAO;

var builder = WebApplication.CreateBuilder(args);

// 1. CONFIGURACIÓN DE SERVICIOS MVC
// Agregamos controladores con vistas y configuramos la ruta personalizada de las carpetas
builder.Services.AddControllersWithViews()
    .AddRazorOptions(options =>
    {
        // Le decimos a .NET que busque en tu carpeta /MVC/View/
        options.ViewLocationFormats.Clear();
        options.ViewLocationFormats.Add("/MVC/View/{1}/{0}.cshtml"); // {1} es Controlador, {0} es Vista
        options.ViewLocationFormats.Add("/MVC/View/Shared/{0}.cshtml");
    });

// 2. CONFIGURACIÓN DE SEGURIDAD (HU-002)
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options => {
        // Ahora los paths apuntan a los Controladores, no a archivos físicos
        options.LoginPath = "/Auth/Login"; 
        options.AccessDeniedPath = "/Home/Index";
        options.ExpireTimeSpan = TimeSpan.FromMinutes(30); // Cierre automático por inactividad
    });
    builder.Services.AddScoped<UsuarioDAO>();
    builder.Services.AddScoped<ClienteDAO>();
    builder.Services.AddScoped<VehiculoDAO>();
    builder.Services.AddScoped<ConductorDAO>();

var app = builder.Build();

// 3. PIPELINE DE SOLICITUDES
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// El orden aquí es vital para la HU-002
app.UseAuthentication(); 
app.UseAuthorization();

// 4. MAPEO DE RUTAS MVC
// Esto reemplaza a MapRazorPages() y define que el Home es el inicio
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();