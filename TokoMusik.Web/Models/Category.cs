
using System.ComponentModel.DataAnnotations;

namespace TokoMusik.Web.Models;

public class Category
{
    public int IdCategory { get; set; }

    [Required(ErrorMessage = "Nama kategori wajib diisi.")]
    [StringLength(100)]
    public string NamaCategory { get; set; } = string.Empty;

    public string? Deskripsi { get; set; }
}
