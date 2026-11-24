
using System.ComponentModel.DataAnnotations;

namespace TokoMusik.Web.Models
{
    public class LaporanInventarisViewModel
    {
        [DataType(DataType.Date)]
        public DateTime TanggalMulai { get; set; }

        [DataType(DataType.Date)]
        public DateTime TanggalSelesai { get; set; }

        public int TotalTransaksiMasuk { get; set; }
        public int TotalTransaksiKeluar { get; set; }

        public int TotalUnitMasuk { get; set; }
        public int TotalUnitKeluar { get; set; }

        public decimal TotalNilaiPembelian { get; set; }

        public int JumlahProdukDirestock { get; set; }
        public int JumlahSupplierAktifTransaksi { get; set; }

        public List<LaporanProdukRestockViewModel> ProdukTerlarisRestock { get; set; } = new();

        public List<LaporanSupplierViewModel> PembelianPerSupplier { get; set; } = new();

        public List<LaporanPergerakanStokViewModel> PergerakanStok { get; set; } = new();
    }

    public class LaporanProdukRestockViewModel
    {
        public int IdProduk { get; set; }
        public string KodeProduk { get; set; } = "";
        public string NamaProduk { get; set; } = "";
        public int TotalUnitMasuk { get; set; }
        public int FrekuensiRestock { get; set; }
        public decimal TotalPembelian { get; set; }
    }

    public class LaporanPergerakanStokViewModel
    {
        public string JenisTransaksi { get; set; } = "";
        public int TotalTransaksi { get; set; }
        public int TotalUnit { get; set; }
    }
}