using System.ComponentModel.DataAnnotations;

namespace TransportesTobonApp.MVC.Models
{
    public class Conductor
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "El nombre es obligatorio.")]
        [StringLength(120)]
        public string Nombre { get; set; } = string.Empty;

        [Required(ErrorMessage = "El documento es obligatorio.")]
        [StringLength(40)]
        public string Documento { get; set; } = string.Empty;

        [Required(ErrorMessage = "La licencia es obligatoria.")]
        [StringLength(40)]
        public string Licencia { get; set; } = string.Empty;

        [StringLength(40)]
        public string? Telefono { get; set; }

        public string Estado { get; set; } = "Disponible";

        public int ServiciosActivos { get; set; }

        public bool EnServicio => ServiciosActivos > 0;

        public static readonly string[] EstadosValidos = new[] { "Disponible", "En descanso", "Incapacitado" };
        public static readonly string[] EstadosNoDisponibles = new[] { "En descanso", "Incapacitado" };
    }
}
