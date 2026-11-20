
using System.ComponentModel.DataAnnotations;

namespace TokoMusik.Web.DTOs;

public class RegisterRequest
{
    [Required]
    [StringLength(50, MinimumLength = 4)]
    public string Username { get; set; } = string.Empty;

    [Required]
    [StringLength(100, MinimumLength = 6)]
    public string Password { get; set; } = string.Empty;

    [Required]
    [Compare(nameof(Password))]
    public string KonfirmasiPassword { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string NamaCustomer { get; set; } = string.Empty;

    [EmailAddress]
    public string? Email { get; set; }

    public string? Telepon { get; set; }
}
