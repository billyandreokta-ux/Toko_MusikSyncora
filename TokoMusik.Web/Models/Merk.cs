
using System.ComponentModel.DataAnnotations;

namespace TokoMusik.Web.Models;

public class Merk
{
    public int IdMerk { get; set; }

    [Required(ErrorMessage = "Nama merek wajib diisi.")]
    [StringLength(100)]
    public string NamaMerk { get; set; } = string.Empty;

    [StringLength(100)]
    public string? Negara { get; set; }
}
