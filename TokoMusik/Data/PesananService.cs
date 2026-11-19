using MySql.Data.MySqlClient;
using TokoMusik.Models;

namespace TokoMusik.Data
{
    public static class PesananService
    {
        // ongkir tetap untuk setiap pesanan (ubah angka ini kalau perlu)
        public const decimal Ongkir = 20000m;

        // pesan yang aman ditampilkan langsung ke user
        public sealed class PesananException : Exception
        {
            public PesananException(string pesan) : base(pesan) { }
        }

        public static int? GetIdCustomer()
        {
            using var conn = Koneksi.GetConnection();
            conn.Open();
            using var cmd = new MySqlCommand("SELECT id_customer FROM customer WHERE id_user=@u", conn);
            cmd.Parameters.AddWithValue("@u", Sesi.IdUser);
            var hasil = cmd.ExecuteScalar();
            if (hasil == null) return null;
            return Convert.ToInt32(hasil);
        }

        public static int AmbilStok(int idProduk)
        {
            using var conn = Koneksi.GetConnection();
            conn.Open();
            using var cmd = new MySqlCommand(
                "SELECT IFNULL(SUM(jumlah), 0) FROM stock WHERE id_produk=@p", conn);
            cmd.Parameters.AddWithValue("@p", idProduk);
            return Convert.ToInt32(cmd.ExecuteScalar());
        }

        // memeriksa kode promo di database, mengembalikan nilai diskon (0 kalau kode kosong)
        public static decimal HitungDiskon(MySqlConnection conn, MySqlTransaction? tx,
            string kode, decimal subtotal, out int? idPromo)
        {
            idPromo = null;
            if (string.IsNullOrWhiteSpace(kode)) return 0;

            using var cmd = new MySqlCommand(
                "SELECT id_promo, tipe, nilai, min_belanja FROM promo " +
                "WHERE kode_promo=@k AND status='aktif' AND CURDATE() BETWEEN tgl_mulai AND tgl_selesai",
                conn, tx);
            cmd.Parameters.AddWithValue("@k", kode.Trim());
            using var r = cmd.ExecuteReader();

            if (!r.Read())
                throw new PesananException("Kode promo tidak valid atau sudah berakhir.");

            int id = r.GetInt32(0);
            string tipe = r.GetString(1);
            decimal nilai = r.GetDecimal(2);
            decimal min = r.IsDBNull(3) ? 0 : r.GetDecimal(3);

            if (subtotal < min)
                throw new PesananException("Promo ini berlaku untuk belanja minimal Rp " + min.ToString("N0") + ".");

            decimal diskon = tipe == "persen" ? Math.Round(subtotal * nilai / 100m, 0) : nilai;
            if (diskon > subtotal) diskon = subtotal;

            idPromo = id;
            return diskon;
        }

        // untuk tampilan ringkasan di keranjang
        public static decimal PreviewDiskon(string kode, decimal subtotal)
        {
            using var conn = Koneksi.GetConnection();
            conn.Open();
            return HitungDiskon(conn, null, kode, subtotal, out _);
        }

        // membuat pesanan dari isi keranjang, mengembalikan nomor pesanan
        public static int Checkout(int idCustomer, int idAlamat, int idMetode, string kodePromo)
        {
            if (Keranjang.Items.Count == 0)
                throw new PesananException("Keranjang masih kosong.");

            using var conn = Koneksi.GetConnection();
            conn.Open();
            using var tx = conn.BeginTransaction();

            // 1. ambil harga dari database (bukan dari keranjang) dan pastikan produk aktif
            var baris = new List<(int id, string nama, decimal harga, int jumlah)>();
            foreach (var it in Keranjang.Items)
            {
                using var cmd = new MySqlCommand(
                    "SELECT nama_produk, harga_jual, status FROM produk WHERE id_produk=@p", conn, tx);
                cmd.Parameters.AddWithValue("@p", it.IdProduk);
                using var r = cmd.ExecuteReader();
                if (!r.Read() || r.GetString(2) != "aktif")
                    throw new PesananException(
                        "Produk \"" + it.Nama + "\" sudah tidak tersedia. Hapus dari keranjang.");
                baris.Add((it.IdProduk, r.GetString(0), r.GetDecimal(1), it.Jumlah));
            }

            // 2. alamat harus milik customer ini
            using (var cek = new MySqlCommand(
                "SELECT COUNT(*) FROM alamat_customer WHERE id_alamat=@a AND id_customer=@c", conn, tx))
            {
                cek.Parameters.AddWithValue("@a", idAlamat);
                cek.Parameters.AddWithValue("@c", idCustomer);
                if (Convert.ToInt32(cek.ExecuteScalar()) == 0)
                    throw new PesananException("Alamat pengiriman tidak valid.");
            }

            // 3. hitung total
            decimal subtotal = baris.Sum(b => b.harga * b.jumlah);
            decimal diskon = HitungDiskon(conn, tx, kodePromo, subtotal, out int? idPromo);
            decimal total = subtotal - diskon + Ongkir;

            // 4. kurangi stok (ditolak kalau tidak cukup)
            foreach (var b in baris)
            {
                using var cmd = new MySqlCommand(
                    "UPDATE stock SET jumlah = jumlah - @q WHERE id_produk=@p AND jumlah >= @q", conn, tx);
                cmd.Parameters.AddWithValue("@q", b.jumlah);
                cmd.Parameters.AddWithValue("@p", b.id);
                if (cmd.ExecuteNonQuery() == 0)
                    throw new PesananException(
                        "Stok \"" + b.nama + "\" tidak cukup. Kurangi jumlahnya di keranjang.");
            }

            // 5. simpan pesanan
            long idPesanan;
            using (var cmd = new MySqlCommand(
                "INSERT INTO pemesanan (id_customer, id_alamat, id_promo, subtotal, diskon, ongkir, total_bayar, status) " +
                "VALUES (@c, @a, @pr, @sub, @dis, @ong, @tot, 'menunggu_bayar')", conn, tx))
            {
                cmd.Parameters.AddWithValue("@c", idCustomer);
                cmd.Parameters.AddWithValue("@a", idAlamat);
                cmd.Parameters.AddWithValue("@pr", (object?)idPromo ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@sub", subtotal);
                cmd.Parameters.AddWithValue("@dis", diskon);
                cmd.Parameters.AddWithValue("@ong", Ongkir);
                cmd.Parameters.AddWithValue("@tot", total);
                cmd.ExecuteNonQuery();
                idPesanan = cmd.LastInsertedId;
            }

            foreach (var b in baris)
            {
                using var cmd = new MySqlCommand(
                    "INSERT INTO detail_pesanan (id_pemesanan, id_produk, jumlah, harga_satuan) " +
                    "VALUES (@o, @p, @q, @h)", conn, tx);
                cmd.Parameters.AddWithValue("@o", idPesanan);
                cmd.Parameters.AddWithValue("@p", b.id);
                cmd.Parameters.AddWithValue("@q", b.jumlah);
                cmd.Parameters.AddWithValue("@h", b.harga);
                cmd.ExecuteNonQuery();
            }

            // 6. catat pembayaran berstatus pending (nanti diverifikasi admin)
            using (var cmd = new MySqlCommand(
                "INSERT INTO payments (id_pemesanan, id_metode, jumlah_bayar, status) " +
                "VALUES (@o, @m, @j, 'pending')", conn, tx))
            {
                cmd.Parameters.AddWithValue("@o", idPesanan);
                cmd.Parameters.AddWithValue("@m", idMetode);
                cmd.Parameters.AddWithValue("@j", total);
                cmd.ExecuteNonQuery();
            }

            tx.Commit();
            Keranjang.Kosongkan();
            return (int)idPesanan;
        }

        // membatalkan pesanan yang belum dibayar, stok dikembalikan
        public static void Batalkan(int idPesanan, int idCustomer)
        {
            using var conn = Koneksi.GetConnection();
            conn.Open();
            using var tx = conn.BeginTransaction();

            object? status;
            using (var cmd = new MySqlCommand(
                "SELECT status FROM pemesanan WHERE id_pemesanan=@id AND id_customer=@c FOR UPDATE", conn, tx))
            {
                cmd.Parameters.AddWithValue("@id", idPesanan);
                cmd.Parameters.AddWithValue("@c", idCustomer);
                status = cmd.ExecuteScalar();
            }

            if (status == null)
                throw new PesananException("Pesanan tidak ditemukan.");
            if (status.ToString() != "menunggu_bayar")
                throw new PesananException("Hanya pesanan yang belum dibayar yang bisa dibatalkan.");

            var detail = new List<(int produk, int jumlah)>();
            using (var cmd = new MySqlCommand(
                "SELECT id_produk, jumlah FROM detail_pesanan WHERE id_pemesanan=@id", conn, tx))
            {
                cmd.Parameters.AddWithValue("@id", idPesanan);
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

            using (var cmd = new MySqlCommand(
                "UPDATE pemesanan SET status='dibatalkan' WHERE id_pemesanan=@id", conn, tx))
            {
                cmd.Parameters.AddWithValue("@id", idPesanan);
                cmd.ExecuteNonQuery();
            }

            using (var cmd = new MySqlCommand(
                "UPDATE payments SET status='gagal' WHERE id_pemesanan=@id AND status='pending'", conn, tx))
            {
                cmd.Parameters.AddWithValue("@id", idPesanan);
                cmd.ExecuteNonQuery();
            }

            tx.Commit();
        }

        // konfirmasi pesanan sudah diterima (hanya dari status dikirim)
        public static void Selesaikan(int idPesanan, int idCustomer)
        {
            using var conn = Koneksi.GetConnection();
            conn.Open();
            using var tx = conn.BeginTransaction();

            using (var cmd = new MySqlCommand(
                "UPDATE pemesanan SET status='selesai' " +
                "WHERE id_pemesanan=@id AND id_customer=@c AND status='dikirim'", conn, tx))
            {
                cmd.Parameters.AddWithValue("@id", idPesanan);
                cmd.Parameters.AddWithValue("@c", idCustomer);
                if (cmd.ExecuteNonQuery() == 0)
                    throw new PesananException("Pesanan hanya bisa dikonfirmasi setelah berstatus dikirim.");
            }

            using (var cmd = new MySqlCommand(
                "UPDATE pengiriman SET status='diterima', tgl_terima=CURDATE() WHERE id_pemesanan=@id", conn, tx))
            {
                cmd.Parameters.AddWithValue("@id", idPesanan);
                cmd.ExecuteNonQuery();
            }

            tx.Commit();
        }
    }
}