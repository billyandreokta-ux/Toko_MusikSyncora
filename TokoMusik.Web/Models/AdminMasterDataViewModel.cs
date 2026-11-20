
using System.ComponentModel.DataAnnotations;

namespace TokoMusik.Web.Models
{
    public class AdminMasterDataViewModel
    {
        public List<MasterDataItemViewModel> Items { get; set; } = new();
        public string Jenis { get; set; } = "";
        public int Total { get; set; }
    }

    public class MasterDataItemViewModel
    {
        public int Id { get; set; }
        public string Nama { get; set; } = "";
        public int JumlahProduk { get; set; }
    }

    public class MasterDataFormViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Nama wajib diisi.")]
        [StringLength(100, ErrorMessage = "Nama maksimal 100 karakter.")]
        public string Nama { get; set; } = "";

        public string Jenis { get; set; } = "";
    }
}