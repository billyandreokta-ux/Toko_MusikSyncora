
using System.ComponentModel.DataAnnotations;

namespace TokoMusik.Web.Models
{
    public class PromoViewModel
    {
        public int IdPromo { get; set; }

        [Required(ErrorMessage = "Kode promo wajib diisi.")]
        [StringLength(30)]
        public string KodePromo { get; set; } = "";

        [Required(ErrorMessage = "Nama promo wajib diisi.")]
        [StringLength(100)]
        public string NamaPromo { get; set; } = "";

        [Required]
        public string Tipe { get; set; } = "persen";

        [Range(1, 100000000, ErrorMessage = "Nilai promo harus lebih dari 0.")]
        public decimal Nilai { get; set; }

        [Range(0, 1000000000)]
        public decimal MinBelanja { get; set; }

        [Required]
        public DateTime TglMulai { get; set; } = DateTime.Today;

        [Required]
        public DateTime TglSelesai { get; set; } = DateTime.Today.AddDays(30);

        public string Status { get; set; } = "aktif";
    }
}
