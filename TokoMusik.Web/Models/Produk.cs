
using System.ComponentModel.DataAnnotations;

namespace TokoMusik.Web.Models
{
    public class Produk
    {
        public int IdProduk { get; set; }

        [Required(ErrorMessage = "Kategori wajib dipilih.")]
        public int IdCategory { get; set; }

        [Required(ErrorMessage = "Merk wajib dipilih.")]
        public int IdMerk { get; set; }

        [Required(ErrorMessage = "Kode produk wajib diisi.")]
        [StringLength(30)]
        public string KodeProduk { get; set; } = "";

        [Required(ErrorMessage = "Nama produk wajib diisi.")]
        [StringLength(150)]
        public string NamaProduk { get; set; } = "";

        [Range(0.01, 999999999999)]
        public decimal HargaJual { get; set; }

        public string? Deskripsi { get; set; }

        public string? Foto { get; set; }

        public string Status { get; set; } = "aktif";
    }
}
