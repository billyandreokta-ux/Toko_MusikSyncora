
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace TokoMusik.Web.Models
{
    public class AdminProdukViewModel
    {
        public List<AdminProdukItemViewModel> Produk { get; set; } = new();
        public int TotalProduk { get; set; }
        public int TotalStokMenipis { get; set; }
        public string? KataKunci { get; set; }
    }

    public class AdminProdukItemViewModel
    {
        public int IdProduk { get; set; }
        public string KodeProduk { get; set; } = "";
        public string NamaProduk { get; set; } = "";
        public string NamaCategory { get; set; } = "";
        public string NamaMerk { get; set; } = "";
        public decimal HargaJual { get; set; }
        public string? Foto { get; set; }
        public string Status { get; set; } = "";
        public int JumlahStok { get; set; }
        public int StokMinimum { get; set; }
    }

    public class AdminProdukFormViewModel
    {
        public int IdProduk { get; set; }

        [Required(ErrorMessage = "Kode produk wajib diisi.")]
        [StringLength(50)]
        public string KodeProduk { get; set; } = "";

        [Required(ErrorMessage = "Nama produk wajib diisi.")]
        [StringLength(150)]
        public string NamaProduk { get; set; } = "";

        [Range(1, int.MaxValue, ErrorMessage = "Pilih kategori.")]
        public int IdCategory { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Pilih merek.")]
        public int IdMerk { get; set; }

        [Range(0, 999999999, ErrorMessage = "Harga tidak valid.")]
        public decimal HargaJual { get; set; }

        [StringLength(2000)]
        public string? Deskripsi { get; set; }

        [Range(0, 1000000)]
        public int JumlahStok { get; set; }

        [Range(0, 1000000)]
        public int StokMinimum { get; set; }

        public string? Foto { get; set; }

        public IFormFile? FotoFile { get; set; }

        public List<PilihanProdukViewModel> Kategori { get; set; } = new();
        public List<PilihanProdukViewModel> Merek { get; set; } = new();
    }

    public class PilihanProdukViewModel
    {
        public int Id { get; set; }
        public string Nama { get; set; } = "";
    }
}