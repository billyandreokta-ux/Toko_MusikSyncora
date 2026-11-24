
using System.ComponentModel.DataAnnotations;

namespace TokoMusik.Web.Models
{
    public class GaransiViewModel
    {
        public List<GaransiItemViewModel> DaftarGaransi { get; set; } = new();
    }

    public class GaransiItemViewModel
    {
        public int IdGaransi { get; set; }
        public int IdDetailPesanan { get; set; }
        public int IdPemesanan { get; set; }
        public string KodeProduk { get; set; } = "";
        public string NamaProduk { get; set; } = "";
        public int Jumlah { get; set; }
        public DateTime TanggalMulai { get; set; }
        public DateTime TanggalBerakhir { get; set; }
        public string? Keterangan { get; set; }
        public string Status { get; set; } = "";
        public string NamaCustomer { get; set; } = "";
    }

    public class KlaimGaransiViewModel
    {
        [Required]
        public int IdGaransi { get; set; }

        [Required(ErrorMessage = "Keterangan klaim wajib diisi.")]
        [StringLength(200, MinimumLength = 5,
            ErrorMessage = "Keterangan harus terdiri dari 5 sampai 200 karakter.")]
        public string KeteranganKlaim { get; set; } = "";

        public string NamaProduk { get; set; } = "";
        public string KodeProduk { get; set; } = "";
        public DateTime TanggalBerakhir { get; set; }
    }

    public class TambahGaransiViewModel
    {
        [Required]
        [Range(1, int.MaxValue, ErrorMessage = "Pilih detail pesanan.")]
        public int IdDetailPesanan { get; set; }

        [Required(ErrorMessage = "Tanggal mulai wajib diisi.")]
        public DateTime TanggalMulai { get; set; } = DateTime.Today;

        [Required(ErrorMessage = "Tanggal berakhir wajib diisi.")]
        public DateTime TanggalBerakhir { get; set; } = DateTime.Today.AddYears(1);

        [StringLength(255)]
        public string? Keterangan { get; set; }

        public List<PilihanDetailGaransiViewModel> DaftarDetailPesanan { get; set; } = new();
    }

    public class PilihanDetailGaransiViewModel
    {
        public int IdDetailPesanan { get; set; }
        public string NamaProduk { get; set; } = "";
        public string KodeProduk { get; set; } = "";
        public int IdPemesanan { get; set; }
        public string NamaCustomer { get; set; } = "";
    }

    public class UbahStatusGaransiViewModel
    {
        [Required]
        public int IdGaransi { get; set; }

        [Required]
        public string Status { get; set; } = "";
    }
}
