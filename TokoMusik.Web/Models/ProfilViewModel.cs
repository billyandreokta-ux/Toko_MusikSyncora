
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace TokoMusik.Web.Models
{
    public class ProfilViewModel
    {
        public int IdCustomer { get; set; }

        [Required(ErrorMessage = "Nama lengkap wajib diisi.")]
        [StringLength(100, ErrorMessage = "Nama maksimal 100 karakter.")]
        [Display(Name = "Nama lengkap")]
        public string NamaCustomer { get; set; } = string.Empty;

        [Required(ErrorMessage = "Username wajib diisi.")]
        [StringLength(50, ErrorMessage = "Username maksimal 50 karakter.")]
        [Display(Name = "Username")]
        public string Username { get; set; } = string.Empty;

        [EmailAddress(ErrorMessage = "Format email tidak valid.")]
        [StringLength(100)]
        [Display(Name = "Email")]
        public string? Email { get; set; }

        [Phone(ErrorMessage = "Format nomor telepon tidak valid.")]
        [StringLength(20)]
        [Display(Name = "Nomor telepon")]
        public string? Telepon { get; set; }

        public string? Foto { get; set; }

        [Display(Name = "Foto profil")]
        public IFormFile? FotoFile { get; set; }
    }
}
