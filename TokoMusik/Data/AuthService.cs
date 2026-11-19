using MySql.Data.MySqlClient;
using TokoMusik.Models;

namespace TokoMusik.Data
{
    public static class AuthService
    {
        // Dipanggil saat aplikasi mulai: membuat akun awal kalau belum ada
        public static void SiapkanAkunAwal()
        {
            using var conn = Koneksi.GetConnection();
            conn.Open();
            PastikanAkun(conn, "admin", "admin123", "admin");
            PastikanAkun(conn, "user1", "user123", "user");
        }

        private static void PastikanAkun(MySqlConnection conn, string username, string password, string role)
        {
            int idUser = 0;
            string hash = "";

            using (var cek = new MySqlCommand("SELECT id_user, password_hash FROM users WHERE username=@u", conn))
            {
                cek.Parameters.AddWithValue("@u", username);
                using var r = cek.ExecuteReader();
                if (r.Read())
                {
                    idUser = r.GetInt32("id_user");
                    hash = r.GetString("password_hash");
                }
            }

            if (idUser == 0)
            {
                using var ins = new MySqlCommand(
                    "INSERT INTO users (username, password_hash, role) VALUES (@u, @p, @r)", conn);
                ins.Parameters.AddWithValue("@u", username);
                ins.Parameters.AddWithValue("@p", BCrypt.Net.BCrypt.HashPassword(password));
                ins.Parameters.AddWithValue("@r", role);
                ins.ExecuteNonQuery();
                idUser = (int)ins.LastInsertedId;
            }
            else if (!hash.StartsWith("$2"))
            {
                // hash masih placeholder dari skrip SQL, ganti dengan hash BCrypt asli
                using var upd = new MySqlCommand(
                    "UPDATE users SET password_hash=@p WHERE id_user=@id", conn);
                upd.Parameters.AddWithValue("@p", BCrypt.Net.BCrypt.HashPassword(password));
                upd.Parameters.AddWithValue("@id", idUser);
                upd.ExecuteNonQuery();
            }

            if (role == "user")
            {
                using var cust = new MySqlCommand(
                    "INSERT IGNORE INTO customer (id_user, nama_customer) VALUES (@id, @n)", conn);
                cust.Parameters.AddWithValue("@id", idUser);
                cust.Parameters.AddWithValue("@n", "Customer " + username);
                cust.ExecuteNonQuery();
            }
        }

        public static bool Login(string username, string password, out string pesan)
        {
            pesan = "";
            try
            {
                using var conn = Koneksi.GetConnection();
                conn.Open();

                using var cmd = new MySqlCommand(
                    "SELECT id_user, password_hash, role, status FROM users WHERE username=@u", conn);
                cmd.Parameters.AddWithValue("@u", username);
                using var r = cmd.ExecuteReader();

                // Satu pesan untuk username salah maupun password salah,
                // supaya tidak membocorkan username mana yang terdaftar.
                if (!r.Read() || !BCrypt.Net.BCrypt.Verify(password, r.GetString("password_hash")))
                {
                    pesan = "Username atau password salah.";
                    return false;
                }

                // Status baru dicek setelah password benar
                if (r.GetString("status") != "aktif")
                {
                    pesan = "Akun tidak aktif. Hubungi admin.";
                    return false;
                }

                Sesi.IdUser = r.GetInt32("id_user");
                Sesi.Username = username;
                Sesi.Role = r.GetString("role");
                return true;
            }
            catch (Exception ex)
            {
                // detail error hanya ke jendela Output Visual Studio, tidak ke user
                System.Diagnostics.Debug.WriteLine(ex);
                pesan = "Terjadi kesalahan saat login. Coba lagi.";
                return false;
            }
        }
    }
}