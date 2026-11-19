using MySql.Data.MySqlClient;

namespace TokoMusik.Data
{
    public static class Koneksi
    {
        // Connection Timeout=5: kalau MySQL mati, gagal dalam 5 detik (bawaan 15 detik)
        // CharSet=utf8mb4: sama dengan charset database db_toko_musik
        private static readonly string connStr =
            "Server=localhost;Port=3306;Database=db_toko_musik;Uid=app_musik;Pwd=billy123;" +
            "Connection Timeout=5;CharSet=utf8mb4;";

        public static MySqlConnection GetConnection()
        {
            return new MySqlConnection(connStr);
        }

        public static bool TestKoneksi(out string pesan)
        {
            try
            {
                using var conn = GetConnection();
                conn.Open();
                pesan = "Koneksi ke database berhasil!";
                return true;
            }
            catch (Exception ex)
            {
                pesan = ex.Message;
                return false;
            }
        }
    }
}