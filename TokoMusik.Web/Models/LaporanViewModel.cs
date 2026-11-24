
namespace TokoMusik.Web.Models
{
    public class LaporanViewModel
    {
        public DateTime TanggalMulai { get; set; }
        public DateTime TanggalSelesai { get; set; }

        // Ringkasan penjualan
        public int TotalPesanan { get; set; }
        public decimal TotalPenjualan { get; set; }
        public int TotalProdukTerjual { get; set; }

        // Ringkasan pembayaran
        public int JumlahPembayaranBerhasil { get; set; }
        public int JumlahPembayaranPending { get; set; }
        public int JumlahPembayaranGagal { get; set; }
        public decimal TotalPembayaranBerhasil { get; set; }

        // Ringkasan pembelian stok
        public int JumlahTransaksiPembelian { get; set; }
        public int TotalUnitDibeli { get; set; }
        public decimal TotalNilaiPembelian { get; set; }

        // Ringkasan pengeluaran
        public int JumlahTransaksiPengeluaran { get; set; }
        public decimal TotalPengeluaran { get; set; }

        public List<LaporanPenjualanItemViewModel> DaftarPenjualan { get; set; } = new();
        public List<LaporanProdukTerlarisViewModel> ProdukTerlaris { get; set; } = new();
        public List<LaporanPembayaranItemViewModel> DaftarPembayaran { get; set; } = new();
        public List<LaporanPembelianItemViewModel> DaftarPembelian { get; set; } = new();
        public List<LaporanSupplierViewModel> PembelianPerSupplier { get; set; } = new();
        public List<LaporanPengeluaranItemViewModel> DaftarPengeluaran { get; set; } = new();
        public List<LaporanKategoriPengeluaranViewModel> PengeluaranPerKategori { get; set; } = new();
    }

    public class LaporanPenjualanItemViewModel
    {
        public int IdPemesanan { get; set; }
        public DateTime Tanggal { get; set; }
        public string NamaCustomer { get; set; } = "";
        public decimal TotalBayar { get; set; }
        public string Status { get; set; } = "";
    }

    public class LaporanProdukTerlarisViewModel
    {
        public string KodeProduk { get; set; } = "";
        public string NamaProduk { get; set; } = "";
        public int JumlahTerjual { get; set; }
        public decimal TotalPenjualan { get; set; }
    }

    public class LaporanPembayaranItemViewModel
    {
        public int IdPayment { get; set; }
        public int IdPemesanan { get; set; }
        public DateTime TanggalBayar { get; set; }
        public int IdMetode { get; set; }
        public decimal JumlahBayar { get; set; }
        public string Status { get; set; } = "";
        public string? BuktiBayar { get; set; }
    }

    public class LaporanPembelianItemViewModel
    {
        public int IdPurchase { get; set; }
        public DateTime Tanggal { get; set; }
        public string NamaSupplier { get; set; } = "";
        public string NamaProduk { get; set; } = "";
        public int Jumlah { get; set; }
        public decimal HargaBeli { get; set; }
        public decimal Total { get; set; }
    }

    // Digunakan bersama oleh laporan penjualan dan laporan inventaris.
    public class LaporanSupplierViewModel
    {
        public int IdSupplier { get; set; }
        public string NamaSupplier { get; set; } = "";

        // Digunakan pada laporan penjualan
        public int JumlahTransaksi { get; set; }

        // Digunakan pada laporan inventaris
        public int TotalTransaksi { get; set; }

        public int TotalUnit { get; set; }
        public decimal TotalPembelian { get; set; }
    }

    public class LaporanPengeluaranItemViewModel
    {
        public int IdPengeluaran { get; set; }
        public DateTime Tanggal { get; set; }
        public string Kategori { get; set; } = "";
        public decimal Jumlah { get; set; }
        public string? Keterangan { get; set; }
    }

    public class LaporanKategoriPengeluaranViewModel
    {
        public string Kategori { get; set; } = "";
        public int JumlahTransaksi { get; set; }
        public decimal TotalPengeluaran { get; set; }
    }
}