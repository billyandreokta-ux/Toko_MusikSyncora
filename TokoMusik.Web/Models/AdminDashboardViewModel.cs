
namespace TokoMusik.Web.Models
{
    public class AdminDashboardViewModel
    {
        public int TotalProduk { get; set; }
        public int TotalPelanggan { get; set; }
        public int TotalPesanan { get; set; }
        public int PesananHariIni { get; set; }
        public int PembayaranMenunggu { get; set; }
        public int ProdukStokMenipis { get; set; }
        public decimal TotalPendapatan { get; set; }

        public List<PesananTerbaruViewModel> PesananTerbaru { get; set; } = new();
        public List<StokMenipisViewModel> StokMenipis { get; set; } = new();
    }

    public class PesananTerbaruViewModel
    {
        public int IdPemesanan { get; set; }
        public string NamaCustomer { get; set; } = "";
        public DateTime TglPesan { get; set; }
        public decimal TotalBayar { get; set; }
        public string Status { get; set; } = "";
    }

    public class StokMenipisViewModel
    {
        public int IdProduk { get; set; }
        public string NamaProduk { get; set; } = "";
        public int Jumlah { get; set; }
        public int StokMinimum { get; set; }
    }
}