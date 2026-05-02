using System.ComponentModel.DataAnnotations;

namespace TransportesTobonApp.MVC.Models
{
    public class Cliente
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "El nombre o razón social es obligatorio.")]
        [StringLength(150)]
        public string Nombre { get; set; }

        [Required(ErrorMessage = "El NIT/CC es obligatorio.")]
        [StringLength(40)]
        public string NitCC { get; set; }

        [StringLength(40)]
        public string? Telefono { get; set; }

        [EmailAddress(ErrorMessage = "Formato de email inválido.")]
        [StringLength(150)]
        public string? Email { get; set; }

        [StringLength(250)]
        public string? Direccion { get; set; }

        public string? Nota { get; set; }

        public bool Estado { get; set; } = true;

        public DateTime FechaCreacion { get; set; }
    }
}
