using System.ComponentModel.DataAnnotations;

namespace TransportesTobonApp.MVC.Models
{
    public class Vehiculo
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "La placa es obligatoria.")]
        [StringLength(20)]
        public string Placa { get; set; } = string.Empty;

        [Required(ErrorMessage = "El modelo es obligatorio.")]
        [StringLength(100)]
        public string Modelo { get; set; } = string.Empty;

        [Range(0, 999, ErrorMessage = "Capacidad inválida.")]
        public decimal? CapacidadToneladas { get; set; }

        public string Estado { get; set; } = "Disponible";

        public DateTime FechaRegistro { get; set; }

        // Calculado: cantidad de servicios sin fecha_fin_real (activos)
        public int ServiciosActivos { get; set; }

        public bool EnServicio => ServiciosActivos > 0;

        public static readonly string[] EstadosValidos = new[] { "Disponible", "Mantenimiento", "Averiado" };
        public static readonly string[] EstadosNoDisponibles = new[] { "Mantenimiento", "Averiado" };
    }
}
