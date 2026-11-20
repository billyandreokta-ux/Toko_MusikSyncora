
using System.ComponentModel.DataAnnotations;

namespace TokoMusik.Web.Models
{
    public class TransaksiStokViewModel
    {
        [Required(ErrorMessage = "Produk wajib dipilih.")]
        [Range(1, int.MaxValue, ErrorMessage = "Pilih produk yang valid.")]
        public int IdProduk { get; set; }

        [Required(ErrorMessage = "Jumlah wajib diisi.")]
        [Range(1, 1000000, ErrorMessage = "Jumlah minimal 1.")]
        public int Jumlah { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Supplier wajib dipilih.")]
        public int IdSupplier { get; set; }

        [Range(0.01, 999999999999, ErrorMessage = "Harga beli harus lebih dari 0.")]
        public decimal HargaBeli { get; set; }

        [StringLength(255, ErrorMessage = "Keterangan maksimal 255 karakter.")]
        public string? Keterangan { get; set; }

        public List<PilihanStokViewModel> DaftarProduk { get; set; } = new();
        public List<PilihanSupplierStokViewModel> DaftarSupplier { get; set; } = new();
    }

    public class TransaksiStokKeluarViewModel
    {
        [Required(ErrorMessage = "Produk wajib dipilih.")]
        [Range(1, int.MaxValue, ErrorMessage = "Pilih produk yang valid.")]
        public int IdProduk { get; set; }

        [Required(ErrorMessage = "Jumlah wajib diisi.")]
        [Range(1, 1000000, ErrorMessage = "Jumlah minimal 1.")]
        public int Jumlah { get; set; }

        [Required(ErrorMessage = "Alasan stok keluar wajib diisi.")]
        [StringLength(255, ErrorMessage = "Alasan maksimal 255 karakter.")]
        public string Keterangan { get; set; } = "";

        public List<PilihanStokViewModel> DaftarProduk { get; set; } = new();
    }

    public class PilihanStokViewModel
    {
        public int IdProduk { get; set; }
        public string NamaProduk { get; set; } = "";
        public string KodeProduk { get; set; } = "";
        public int JumlahStok { get; set; }
    }

    public class PilihanSupplierStokViewModel
    {
        public int IdSupplier { get; set; }
        public string NamaSupplier { get; set; } = "";
    }

    public class RiwayatStokViewModel
    {
        public List<RiwayatStokItemViewModel> Transaksi { get; set; } = new();
    }

    public class RiwayatStokItemViewModel
    {
        public string JenisTransaksi { get; set; } = "";
        public DateTime Tanggal { get; set; }
        public string KodeProduk { get; set; } = "";
        public string NamaProduk { get; set; } = "";
        public int Jumlah { get; set; }
        public decimal HargaBeli { get; set; }
        public decimal Total { get; set; }
        public string Keterangan { get; set; } = "";
        public string? NamaSupplier { get; set; }
    }
}
