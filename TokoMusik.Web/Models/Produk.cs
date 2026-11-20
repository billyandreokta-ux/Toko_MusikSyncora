
using System.ComponentModel.DataAnnotations;

namespace TokoMusik.Web.Models;

public class Produk
{
    public int IdProduk { get; set; }

    [Required]
    public int IdCategory { get; set; }

    [Required]
    public int IdMerk { get; set; }

    [Required(ErrorMessage = "Kode produk wajib diisi.")]
    [StringLength(30)]
    public string KodeProduk { get; set; } = string.Empty;

    [Required(ErrorMessage = "Nama produk wajib diisi.")]
    [StringLength(150)]
    public string NamaProduk { get; set; } = string.Empty;

    [Range(0.01, 999999999999)]
    public decimal HargaJual { get; set; }

    public string? Deskripsi { get; set; }

    public string? Foto { get; set; }

    [RegularExpression("^(aktif|nonaktif)$")]
    public string Status { get; set; } = "aktif";

    public string? NamaCategory { get; set; }

    public string? NamaMerk { get; set; }

    public int JumlahStok { get; set; }
}
