
using MySqlConnector;

namespace TokoMusik.Web.Data;

public class Koneksi
{
    private readonly IConfiguration _configuration;

    public Koneksi(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public MySqlConnection GetConnection()
    {
        string connectionString =
            _configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "Connection string tidak ditemukan."
            );

        return new MySqlConnection(connectionString);
    }
}
