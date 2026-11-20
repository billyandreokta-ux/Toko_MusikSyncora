
using System.ComponentModel.DataAnnotations;

namespace TokoMusik.Web.Models
{
    public class AlamatCustomer
    {
        public int IdAlamat { get; set; }

        public int IdCustomer { get; set; }

        [Required(ErrorMessage = "Label alamat wajib diisi.")]
        [StringLength(50)]
        public string LabelAlamat { get; set; } = "Rumah";

        [Required(ErrorMessage = "Nama penerima wajib diisi.")]
        [StringLength(100)]
        public string Penerima { get; set; } = "";

        [Required(ErrorMessage = "Nomor telepon wajib diisi.")]
        [StringLength(20)]
        public string Telepon { get; set; } = "";

        [Required(ErrorMessage = "Alamat lengkap wajib diisi.")]
        public string AlamatLengkap { get; set; } = "";

        [Required(ErrorMessage = "Kota wajib diisi.")]
        [StringLength(100)]
        public string Kota { get; set; } = "";

        [Required(ErrorMessage = "Provinsi wajib diisi.")]
        [StringLength(100)]
        public string Provinsi { get; set; } = "";

        [Required(ErrorMessage = "Kode pos wajib diisi.")]
        [StringLength(10)]
        public string KodePos { get; set; } = "";

        public bool IsUtama { get; set; }
    }
}
