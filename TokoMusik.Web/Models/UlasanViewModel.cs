
using System.ComponentModel.DataAnnotations;

namespace TokoMusik.Web.Models
{
    public class UlasanViewModel
    {
        public int IdProduk { get; set; }
        public string NamaProduk { get; set; } = "";
        public string KodeProduk { get; set; } = "";
        public string? Foto { get; set; }

        [Range(1, 5, ErrorMessage = "Rating harus antara 1 sampai 5.")]
        public int Rating { get; set; } = 5;

        [StringLength(1000, ErrorMessage = "Komentar maksimal 1000 karakter.")]
        public string? Komentar { get; set; }
    }

    public class UlasanRiwayatViewModel
    {
        public int IdUlasan { get; set; }
        public string NamaProduk { get; set; } = "";
        public int Rating { get; set; }
        public string? Komentar { get; set; }
        public DateTime TglUlasan { get; set; }
    }

    public class UlasanHalamanViewModel
    {
        public List<UlasanViewModel> ProdukBelumDiulas { get; set; } = new();
        public List<UlasanRiwayatViewModel> RiwayatUlasan { get; set; } = new();
    }
}
