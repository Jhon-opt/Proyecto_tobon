namespace TransportesTobonApp.MVC.Models
{
    public class Usuario
    {
        public int Id { get; set; }
        public string Nombre { get; set; }
        public string Email { get; set; }
        public string Rol { get; set; }
        public string? Password { get; set; }
        public bool Estado { get; set; }
        public DateTime FechaCreacion { get; set; }
    }
}
