
using System.ComponentModel.DataAnnotations;

namespace TokoMusik.Web.Models
{
    public class TarifPengirimanViewModel
    {
        public int IdTarif { get; set; }

        [Required(ErrorMessage = "Kota wajib diisi.")]
        [StringLength(100)]
        public string Kota { get; set; } = string.Empty;

        [Required(ErrorMessage = "Provinsi wajib diisi.")]
        [StringLength(100)]
        public string Provinsi { get; set; } = string.Empty;

        [Range(0, 999999999999, ErrorMessage = "Biaya pengiriman tidak valid.")]
        public decimal Biaya { get; set; }

        [StringLength(50)]
        public string? EstimasiHari { get; set; }

        public string Status { get; set; } = "aktif";
    }
}
