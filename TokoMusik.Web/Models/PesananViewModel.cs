namespace TokoMusik.Web.Models
{
    public class PesananViewModel
    {
        public int IdPemesanan { get; set; }
        public DateTime TglPesan { get; set; }
        public decimal TotalBayar { get; set; }
        public string StatusPesanan { get; set; } = "";
        public string StatusPembayaran { get; set; } = "";
        public string StatusTampilan { get; set; } = "";
        public string NamaMetode { get; set; } = "";
        public List<DetailPesananViewModel> Items { get; set; } = new();
    }

    public class DetailPesananViewModel
    {
        public int IdPemesanan { get; set; }
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

        public DateTime? WaktuDiproses { get; set; }
        public DateTime? WaktuKirim { get; set; }
        public DateTime? WaktuTerima { get; set; }

        public DateTime? TglDibatalkan { get; set; }
        public string? AlasanPembatalan { get; set; }

        public DateTime? BatasRetur { get; set; }
        public bool DapatRetur { get; set; }
        public bool DapatDibatalkan { get; set; }

        public List<string> PilihanAlasanPembatalan { get; set; } = new();

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

    public class BatalkanPesananViewModel
    {
        public int IdPemesanan { get; set; }

        public DateTime TglPesan { get; set; }

        public decimal TotalBayar { get; set; }

        public string StatusPesanan { get; set; } = "";

        public string StatusPembayaran { get; set; } = "";

        public string AlasanPembatalan { get; set; } = "";

        public string? KeteranganTambahan { get; set; }
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