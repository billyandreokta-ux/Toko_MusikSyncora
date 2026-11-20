
namespace TokoMusik.Web.Models
{
    public class PesananViewModel
    {
        public int IdPemesanan { get; set; }
        public DateTime TglPesan { get; set; }
        public decimal TotalBayar { get; set; }
        public string StatusPesanan { get; set; } = "";
        public string StatusPembayaran { get; set; } = "";
        public string NamaMetode { get; set; } = "";
    }

    public class DetailPesananViewModel
    {
        public int IdDetailPesanan { get; set; }
        public int IdProduk { get; set; }
        public string NamaProduk { get; set; } = "";
        public string KodeProduk { get; set; } = "";
        public string? Foto { get; set; }
        public int Jumlah { get; set; }
        public decimal HargaSatuan { get; set; }
        public decimal Subtotal { get; set; }
    }

    public class PesananSayaDetailViewModel
    {
        public int IdPemesanan { get; set; }
        public DateTime TglPesan { get; set; }
        public decimal Subtotal { get; set; }
        public decimal Diskon { get; set; }
        public decimal Ongkir { get; set; }
        public decimal TotalBayar { get; set; }
        public string StatusPesanan { get; set; } = "";
        public string StatusPembayaran { get; set; } = "";
        public string NamaMetode { get; set; } = "";
        public string? BuktiBayar { get; set; }
        public string NamaPenerima { get; set; } = "";
        public string Telepon { get; set; } = "";
        public string AlamatLengkap { get; set; } = "";
        public string Kota { get; set; } = "";
        public string Provinsi { get; set; } = "";
        public string KodePos { get; set; } = "";
        public List<DetailPesananViewModel> Items { get; set; } = new();
    }

    public class AdminPembayaranViewModel
    {
        public int IdPayment { get; set; }
        public int IdPemesanan { get; set; }
        public string Username { get; set; } = "";
        public string NamaCustomer { get; set; } = "";
        public string NamaMetode { get; set; } = "";
        public DateTime TglBayar { get; set; }
        public decimal JumlahBayar { get; set; }
        public string? BuktiBayar { get; set; }
        public string StatusPembayaran { get; set; } = "";
        public string StatusPesanan { get; set; } = "";
    }

    public class AdminPembayaranDetailViewModel
    {
        public int IdPayment { get; set; }
        public int IdPemesanan { get; set; }
        public string Username { get; set; } = "";
        public string NamaCustomer { get; set; } = "";
        public string Telepon { get; set; } = "";
        public string NamaMetode { get; set; } = "";
        public DateTime TglBayar { get; set; }
        public decimal JumlahBayar { get; set; }
        public string? BuktiBayar { get; set; }
        public string StatusPembayaran { get; set; } = "";
        public string StatusPesanan { get; set; } = "";
        public decimal TotalPesanan { get; set; }
    }
}
