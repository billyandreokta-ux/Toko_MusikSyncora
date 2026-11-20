
namespace TokoMusik.Web.Models
{
    public class AdminPesananViewModel
    {
        public int IdPemesanan { get; set; }
        public string NamaCustomer { get; set; } = "";
        public string Username { get; set; } = "";
        public DateTime TglPesan { get; set; }
        public decimal TotalBayar { get; set; }
        public string Status { get; set; } = "";
        public string StatusPembayaran { get; set; } = "";
    }

    public class AdminPesananDetailViewModel
    {
        public int IdPemesanan { get; set; }
        public string NamaCustomer { get; set; } = "";
        public string Username { get; set; } = "";
        public string Telepon { get; set; } = "";
        public DateTime TglPesan { get; set; }
        public decimal Subtotal { get; set; }
        public decimal Diskon { get; set; }
        public decimal Ongkir { get; set; }
        public decimal TotalBayar { get; set; }
        public string Status { get; set; } = "";
        public string StatusPembayaran { get; set; } = "";
        public string NamaPenerima { get; set; } = "";
        public string TeleponPenerima { get; set; } = "";
        public string AlamatLengkap { get; set; } = "";
        public string Kota { get; set; } = "";
        public string Provinsi { get; set; } = "";
        public string KodePos { get; set; } = "";
        public string? Kurir { get; set; }
        public string? NoResi { get; set; }
        public string? StatusPengiriman { get; set; }
        public List<AdminPesananItemViewModel> Items { get; set; } = new();
    }

    public class AdminPesananItemViewModel
    {
        public string NamaProduk { get; set; } = "";
        public string KodeProduk { get; set; } = "";
        public int Jumlah { get; set; }
        public decimal HargaSatuan { get; set; }
        public decimal Subtotal { get; set; }
    }
}
