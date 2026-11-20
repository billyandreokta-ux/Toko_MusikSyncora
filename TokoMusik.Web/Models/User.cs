
namespace TokoMusik.Web.Models;

public class User
{
    public int IdUser { get; set; }

    public string Username { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public string Role { get; set; } = "user";

    public string Status { get; set; } = "aktif";

    public DateTime CreatedAt { get; set; }
}
