
using System.ComponentModel.DataAnnotations;

namespace TokoMusik.Web.Models
{
    public class SupplierViewModel
    {
        public int IdSupplier { get; set; }

        [Required(ErrorMessage = "Nama supplier wajib diisi.")]
        [StringLength(150, ErrorMessage = "Nama supplier maksimal 150 karakter.")]
        public string NamaSupplier { get; set; } = string.Empty;

        [Phone(ErrorMessage = "Format nomor telepon tidak valid.")]
        [StringLength(20)]
        public string? Telepon { get; set; }

        [EmailAddress(ErrorMessage = "Format email tidak valid.")]
        [StringLength(100)]
        public string? Email { get; set; }

        public string? Alamat { get; set; }
    }

    public class SupplierIndexViewModel
    {
        public string KataKunci { get; set; } = string.Empty;

        public int TotalSupplier { get; set; }

        public int TotalTransaksiPembelian { get; set; }

        public decimal TotalNilaiPembelian { get; set; }

        public List<SupplierViewModel> DaftarSupplier { get; set; } = new();
    }

    public class SupplierDetailViewModel
    {
        public SupplierViewModel Supplier { get; set; } = new();

        public int TotalTransaksi { get; set; }

        public int TotalProdukDibeli { get; set; }

        public decimal TotalPembelian { get; set; }

        public List<RiwayatPembelianSupplierViewModel> RiwayatPembelian { get; set; } = new();
    }

    public class RiwayatPembelianSupplierViewModel
    {
        public int IdPurchase { get; set; }

        public DateTime Tanggal { get; set; }

        public string NamaProduk { get; set; } = string.Empty;

        public string KodeProduk { get; set; } = string.Empty;

        public int Jumlah { get; set; }

        public decimal HargaBeli { get; set; }

        public decimal Total { get; set; }

        public string? Keterangan { get; set; }
    }
}
