
namespace TokoMusik.Web.Models
{
    public class AkunTersimpanViewModel
    {
        public int IdUser { get; set; }

        public string Username { get; set; } = string.Empty;

        public string Role { get; set; } = string.Empty;

        public string? NamaCustomer { get; set; }

        public string? Foto { get; set; }
    }
}
