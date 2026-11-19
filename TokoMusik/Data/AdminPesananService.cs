using MySql.Data.MySqlClient;

namespace TokoMusik.Data
{
    public static class AdminPesananService
    {
        private static string AmbilStatus(MySqlConnection conn, MySqlTransaction tx, int id)
        {
            using var cmd = new MySqlCommand(
                "SELECT status FROM pemesanan WHERE id_pemesanan=@id FOR UPDATE", conn, tx);
            cmd.Parameters.AddWithValue("@id", id);
            var hasil = cmd.ExecuteScalar();
            if (hasil == null)
                throw new PesananService.PesananException("Pesanan tidak ditemukan.");
            return hasil.ToString() ?? "";
        }

        private static void SetStatus(MySqlConnection conn, MySqlTransaction tx, int id, string status)
        {
            using var cmd = new MySqlCommand(
                "UPDATE pemesanan SET status=@s WHERE id_pemesanan=@id", conn, tx);
            cmd.Parameters.AddWithValue("@s", status);
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }

        // menunggu_bayar -> dibayar
        public static void VerifikasiPembayaran(int id)
        {
            using var conn = Koneksi.GetConnection();
            conn.Open();
            using var tx = conn.BeginTransaction();

            if (AmbilStatus(conn, tx, id) != "menunggu_bayar")
                throw new PesananService.PesananException(
                    "Hanya pesanan yang menunggu pembayaran yang bisa diverifikasi.");

            object? idPay;
            using (var cmd = new MySqlCommand(
                "SELECT id_payment FROM payments WHERE id_pemesanan=@id AND status='pending' " +
                "ORDER BY id_payment DESC LIMIT 1", conn, tx))
            {
                cmd.Parameters.AddWithValue("@id", id);
                idPay = cmd.ExecuteScalar();
            }
            if (idPay == null)
                throw new PesananService.PesananException(
                    "Data pembayaran yang menunggu verifikasi tidak ditemukan.");

            using (var cmd = new MySqlCommand(
                "UPDATE payments SET status='berhasil', tgl_bayar=NOW() WHERE id_payment=@p", conn, tx))
            {
                cmd.Parameters.AddWithValue("@p", Convert.ToInt32(idPay));
                cmd.ExecuteNonQuery();
            }

            SetStatus(conn, tx, id, "dibayar");
            tx.Commit();
        }

        // dibayar -> diproses
        public static void Proses(int id)
        {
            using var conn = Koneksi.GetConnection();
            conn.Open();
            using var tx = conn.BeginTransaction();

            if (AmbilStatus(conn, tx, id) != "dibayar")
                throw new PesananService.PesananException(
                    "Pesanan harus berstatus dibayar sebelum diproses. Verifikasi pembayarannya dulu.");

            SetStatus(conn, tx, id, "diproses");
            tx.Commit();
        }

        // diproses -> dikirim (data pengiriman disimpan atau diperbarui)
        public static void Kirim(int id, string kurir, string resi, DateTime tglKirim)
        {
            using var conn = Koneksi.GetConnection();
            conn.Open();
            using var tx = conn.BeginTransaction();

            if (AmbilStatus(conn, tx, id) != "diproses")
                throw new PesananService.PesananException(
                    "Pesanan harus berstatus diproses sebelum dikirim.");

            using (var cmd = new MySqlCommand(
                "INSERT INTO pengiriman (id_pemesanan, kurir, no_resi, tgl_kirim, status) " +
                "VALUES (@id, @k, @r, @t, 'dikirim') " +
                "ON DUPLICATE KEY UPDATE kurir=@k, no_resi=@r, tgl_kirim=@t, status='dikirim'", conn, tx))
            {
                cmd.Parameters.AddWithValue("@id", id);
                cmd.Parameters.AddWithValue("@k", kurir.Trim());
                cmd.Parameters.AddWithValue("@r", resi.Trim());
                cmd.Parameters.AddWithValue("@t", tglKirim.Date);
                cmd.ExecuteNonQuery();
            }

            SetStatus(conn, tx, id, "dikirim");
            tx.Commit();
        }

        // dikirim -> selesai (dipakai kalau customer tidak mengonfirmasi sendiri)
        public static void Selesaikan(int id)
        {
            using var conn = Koneksi.GetConnection();
            conn.Open();
            using var tx = conn.BeginTransaction();

            if (AmbilStatus(conn, tx, id) != "dikirim")
                throw new PesananService.PesananException(
                    "Pesanan hanya bisa ditandai selesai setelah berstatus dikirim.");

            using (var cmd = new MySqlCommand(
                "UPDATE pengiriman SET status='diterima', tgl_terima=CURDATE() WHERE id_pemesanan=@id", conn, tx))
            {
                cmd.Parameters.AddWithValue("@id", id);
                cmd.ExecuteNonQuery();
            }

            SetStatus(conn, tx, id, "selesai");
            tx.Commit();
        }

        // membatalkan pesanan yang belum dikirim, stok dikembalikan.
        // mengembalikan true kalau pembayarannya sudah pernah diterima (dana perlu dikembalikan manual)
        public static bool Batalkan(int id)
        {
            using var conn = Koneksi.GetConnection();
            conn.Open();
            using var tx = conn.BeginTransaction();

            string status = AmbilStatus(conn, tx, id);
            if (status != "menunggu_bayar" && status != "dibayar" && status != "diproses")
                throw new PesananService.PesananException(
                    "Pesanan dengan status \"" + status + "\" tidak bisa dibatalkan.");

            var detail = new List<(int produk, int jumlah)>();
            using (var cmd = new MySqlCommand(
                "SELECT id_produk, jumlah FROM detail_pesanan WHERE id_pemesanan=@id", conn, tx))
            {
                cmd.Parameters.AddWithValue("@id", id);
                using var r = cmd.ExecuteReader();
                while (r.Read())
                    detail.Add((r.GetInt32(0), r.GetInt32(1)));
            }

            foreach (var d in detail)
            {
                using var cmd = new MySqlCommand(
                    "INSERT INTO stock (id_produk, jumlah) VALUES (@p, @q) " +
                    "ON DUPLICATE KEY UPDATE jumlah = jumlah + @q", conn, tx);
                cmd.Parameters.AddWithValue("@p", d.produk);
                cmd.Parameters.AddWithValue("@q", d.jumlah);
                cmd.ExecuteNonQuery();
            }

            bool sudahDibayar;
            using (var cmd = new MySqlCommand(
                "SELECT COUNT(*) FROM payments WHERE id_pemesanan=@id AND status='berhasil'", conn, tx))
            {
                cmd.Parameters.AddWithValue("@id", id);
                sudahDibayar = Convert.ToInt32(cmd.ExecuteScalar()) > 0;
            }

            using (var cmd = new MySqlCommand(
                "UPDATE payments SET status='gagal' WHERE id_pemesanan=@id AND status='pending'", conn, tx))
            {
                cmd.Parameters.AddWithValue("@id", id);
                cmd.ExecuteNonQuery();
            }

            SetStatus(conn, tx, id, "dibatalkan");
            tx.Commit();
            return sudahDibayar;
        }
    }
}