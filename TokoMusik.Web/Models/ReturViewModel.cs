
using System.ComponentModel.DataAnnotations;

namespace TokoMusik.Web.Models
{
    public class ReturViewModel
    {
        public List<ReturItemViewModel> DaftarRetur { get; set; } = new();
        public List<ProdukReturViewModel> ProdukDapatDiretur { get; set; } = new();
    }

    public class ReturItemViewModel
    {
        public int IdReturn { get; set; }
        public int IdDetailPesanan { get; set; }
        public DateTime TanggalReturn { get; set; }
        public DateTime? TanggalPengajuan { get; set; }
        public string KodeProduk { get; set; } = "";
        public string NamaProduk { get; set; } = "";
        public int Jumlah { get; set; }
        public string Alasan { get; set; } = "";
        public string Status { get; set; } = "";
        public int IdPemesanan { get; set; }
        public string NamaCustomer { get; set; } = "";

        public string? FotoBukti { get; set; }
        public bool BarangDiterima { get; set; }
        public bool? LayakJual { get; set; }
        public bool StokDitambahkan { get; set; }
        public DateTime? TanggalPemeriksaan { get; set; }
        public string? CatatanAdmin { get; set; }
    }

    public class ProdukReturViewModel
    {
        public int IdDetailPesanan { get; set; }
        public int IdPemesanan { get; set; }
        public string KodeProduk { get; set; } = "";
        public string NamaProduk { get; set; } = "";
        public int JumlahDibeli { get; set; }
        public int JumlahSudahDiretur { get; set; }
        public int SisaJumlah { get; set; }
        public string StatusPesanan { get; set; } = "";
        public DateTime? WaktuTerima { get; set; }
    }

    public class AjukanReturViewModel
    {
        [Required]
        public int IdDetailPesanan { get; set; }

        [Required(ErrorMessage = "Jumlah retur wajib diisi.")]
        [Range(1, 1000000, ErrorMessage = "Jumlah retur minimal 1.")]
        public int Jumlah { get; set; }

        [Required(ErrorMessage = "Alasan retur wajib diisi.")]
        [StringLength(2000, MinimumLength = 5,
            ErrorMessage = "Alasan harus terdiri dari 5 sampai 2000 karakter.")]
        public string Alasan { get; set; } = "";

        [Required(ErrorMessage = "Foto bukti retur wajib diunggah.")]
        public IFormFile? FotoBukti { get; set; }

        public string NamaProduk { get; set; } = "";
        public string KodeProduk { get; set; } = "";
        public int SisaJumlah { get; set; }
    }

    public class UbahStatusReturViewModel
    {
        [Required]
        public int IdReturn { get; set; }

        [Required]
        public string Status { get; set; } = "";
    }
}
