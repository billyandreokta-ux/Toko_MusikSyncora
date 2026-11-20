
namespace TokoMusik.Web.Models
{
    public class KeranjangViewModel
    {
        public int IdProduk { get; set; }

        public string NamaProduk { get; set; } = "";

        public string KodeProduk { get; set; } = "";

        public string? Foto { get; set; }

        public decimal HargaJual { get; set; }

        public int Jumlah { get; set; }

        public int Stok { get; set; }

        public decimal Subtotal => HargaJual * Jumlah;
    }
}
