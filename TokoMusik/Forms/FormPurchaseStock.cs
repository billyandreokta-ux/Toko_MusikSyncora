using System.Data;
using System.Globalization;
using MySql.Data.MySqlClient;
using TokoMusik.Data;

namespace TokoMusik.Forms
{
    public class FormPurchaseStock : Form
    {
        private readonly DateTimePicker dtpTanggal = new();
        private readonly ComboBox cbSupplier = new();
        private readonly ComboBox cbProduk = new();
        private readonly ComboBox cbKaryawan = new();
        private readonly TextBox txtJumlah = new();
        private readonly TextBox txtHarga = new();
        private readonly TextBox txtKeterangan = new();
        private readonly TextBox txtCari = new();
        private readonly Label lblStok = new();
        private readonly Label lblTotal = new();
        private readonly DataGridView grid = new();

        private int idTerpilih = 0;

        // dilempar saat perubahan akan membuat stok menjadi minus
        private sealed class StokException : Exception
        {
            public StokException(string pesan) : base(pesan) { }
        }

        public FormPurchaseStock()
        {
            Text = "Pembelian Stok dari Supplier";
            ClientSize = new Size(1000, 640);
            MinimumSize = new Size(900, 560);
            StartPosition = FormStartPosition.CenterParent;
            Font = new Font("Segoe UI", 10F);

            txtJumlah.MaxLength = 6;
            txtHarga.MaxLength = 10;
            txtKeterangan.MaxLength = 255;
            dtpTanggal.Format = DateTimePickerFormat.Short;

            // ---------- kolom input ----------
            TambahInput("Tanggal *", dtpTanggal, 20, 10, 220);
            TambahInput("Supplier *", cbSupplier, 260, 10, 340);
            TambahInput("Produk *", cbProduk, 620, 10, 360);
            TambahInput("Jumlah Beli *", txtJumlah, 20, 64, 220);
            TambahInput("Harga Beli per Unit (Rp) *", txtHarga, 260, 64, 340);
            TambahInput("Karyawan (penerima barang)", cbKaryawan, 620, 64, 360);
            TambahInput("Keterangan", txtKeterangan, 20, 118, 580);

            lblStok.Bounds = new Rectangle(620, 122, 360, 22);
            lblStok.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            lblTotal.Bounds = new Rectangle(620, 148, 360, 22);
            lblTotal.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            Controls.Add(lblStok);
            Controls.Add(lblTotal);

            // ---------- tombol aksi ----------
            Controls.Add(BuatTombol("Tambah", 20, 186, BtnTambah_Click));
            Controls.Add(BuatTombol("Ubah", 140, 186, BtnUbah_Click));
            Controls.Add(BuatTombol("Hapus", 260, 186, BtnHapus_Click));
            Controls.Add(BuatTombol("Bersihkan", 380, 186, (s, e) => Bersihkan()));

            // ---------- pencarian ----------
            Controls.Add(new Label { Text = "Cari:", Bounds = new Rectangle(20, 240, 50, 22) });
            txtCari.Bounds = new Rectangle(70, 237, 300, 28);
            txtCari.TextChanged += (s, e) => MuatData();
            Controls.Add(txtCari);

            // ---------- tabel ----------
            grid.Bounds = new Rectangle(20, 276, 960, 344);
            grid.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            grid.ReadOnly = true;
            grid.AllowUserToAddRows = false;
            grid.AllowUserToDeleteRows = false;
            grid.MultiSelect = false;
            grid.RowHeadersVisible = false;
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            grid.BackgroundColor = Color.White;
            grid.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.True;
            grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            grid.CellClick += Grid_CellClick;
            grid.DataBindingComplete += (s, e) => grid.ClearSelection();
            Controls.Add(grid);

            // ---------- isi pilihan ----------
            try
            {
                IsiCombo(cbSupplier, "SELECT id_supplier, nama_supplier FROM supplier ORDER BY nama_supplier");
                IsiCombo(cbProduk,
                    "SELECT id_produk, CONCAT(kode_produk, ' - ', nama_produk) FROM produk ORDER BY nama_produk");
                IsiCombo(cbKaryawan,
                    "SELECT id_karyawan, nama_karyawan FROM karyawan ORDER BY nama_karyawan", "(tidak ada)");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex);
                MessageBox.Show("Gagal memuat pilihan supplier, produk, atau karyawan. Coba lagi.");
            }

            // event dipasang setelah combo terisi
            cbProduk.SelectedIndexChanged += (s, e) => TampilStok();
            txtJumlah.TextChanged += (s, e) => HitungTotal();
            txtHarga.TextChanged += (s, e) => HitungTotal();

            Bersihkan();
            Load += (s, e) => MuatData();
        }

        // ================= tata letak =================
        private void TambahInput(string label, Control kontrol, int x, int y, int lebar)
        {
            Controls.Add(new Label { Text = label, Bounds = new Rectangle(x, y, lebar, 20) });
            kontrol.Bounds = new Rectangle(x, y + 22, lebar, 27);
            Controls.Add(kontrol);
        }

        private static Button BuatTombol(string teks, int x, int y, EventHandler aksi)
        {
            var b = new Button { Text = teks, Bounds = new Rectangle(x, y, 110, 34) };
            b.Click += aksi;
            return b;
        }

        private static void IsiCombo(ComboBox cb, string sql, string? teksKosong = null)
        {
            var dt = new DataTable();
            dt.Columns.Add("Id", typeof(int));
            dt.Columns.Add("Nama", typeof(string));
            if (teksKosong != null) dt.Rows.Add(0, teksKosong);

            using var conn = Koneksi.GetConnection();
            conn.Open();
            using var cmd = new MySqlCommand(sql, conn);
            using var r = cmd.ExecuteReader();
            while (r.Read())
                dt.Rows.Add(r.GetInt32(0), r.GetString(1));

            cb.DropDownStyle = ComboBoxStyle.DropDownList;
            cb.DataSource = dt;
            cb.DisplayMember = "Nama";
            cb.ValueMember = "Id";
        }

        // ================= info stok dan total =================
        private void TampilStok()
        {
            if (cbProduk.SelectedValue is not int idProduk)
            {
                lblStok.Text = "";
                return;
            }

            try
            {
                using var conn = Koneksi.GetConnection();
                conn.Open();
                using var cmd = new MySqlCommand(
                    "SELECT IFNULL(SUM(jumlah), 0) FROM stock WHERE id_produk=@id", conn);
                cmd.Parameters.AddWithValue("@id", idProduk);
                int stok = Convert.ToInt32(cmd.ExecuteScalar());
                lblStok.Text = "Stok saat ini: " + stok.ToString("N0");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex);
                lblStok.Text = "";
            }
        }

        private void HitungTotal()
        {
            if (int.TryParse(txtJumlah.Text.Trim(), NumberStyles.None, CultureInfo.CurrentCulture, out int j) &&
                decimal.TryParse(txtHarga.Text.Trim(), NumberStyles.None, CultureInfo.CurrentCulture, out decimal h))
                lblTotal.Text = "Total: Rp " + (j * h).ToString("N0");
            else
                lblTotal.Text = "";
        }

        // ================= tampil data =================
        private void MuatData()
        {
            try
            {
                using var conn = Koneksi.GetConnection();
                conn.Open();
                using var da = new MySqlDataAdapter(
                    "SELECT ps.id_purchase AS ID, ps.tanggal AS Tanggal, s.nama_supplier AS Supplier, " +
                    "p.nama_produk AS Produk, ps.jumlah AS Jumlah, ps.harga_beli AS `Harga Beli`, " +
                    "ps.total AS Total, IFNULL(k.nama_karyawan, '-') AS Karyawan " +
                    "FROM purchase_stock ps " +
                    "JOIN supplier s ON s.id_supplier = ps.id_supplier " +
                    "JOIN produk p ON p.id_produk = ps.id_produk " +
                    "LEFT JOIN karyawan k ON k.id_karyawan = ps.id_karyawan " +
                    "WHERE p.nama_produk LIKE @cari OR s.nama_supplier LIKE @cari " +
                    "ORDER BY ps.id_purchase DESC", conn);
                da.SelectCommand.Parameters.AddWithValue("@cari", "%" + txtCari.Text.Trim() + "%");
                var dt = new DataTable();
                da.Fill(dt);
                grid.DataSource = dt;

                if (grid.Columns.Contains("ID")) grid.Columns["ID"]!.FillWeight = 25;
                if (grid.Columns.Contains("Jumlah")) grid.Columns["Jumlah"]!.FillWeight = 40;
                if (grid.Columns.Contains("Tanggal")) grid.Columns["Tanggal"]!.DefaultCellStyle.Format = "dd/MM/yyyy";
                foreach (var nama in new[] { "Harga Beli", "Total" })
                {
                    if (!grid.Columns.Contains(nama)) continue;
                    grid.Columns[nama]!.DefaultCellStyle.Format = "N0";
                    grid.Columns[nama]!.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex);
                MessageBox.Show("Gagal memuat data. Coba lagi.");
            }
        }

        // ================= klik baris: isi form =================
        private void Grid_CellClick(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;
            idTerpilih = Convert.ToInt32(grid.Rows[e.RowIndex].Cells["ID"].Value);

            try
            {
                using var conn = Koneksi.GetConnection();
                conn.Open();
                using var cmd = new MySqlCommand(
                    "SELECT * FROM purchase_stock WHERE id_purchase=@id", conn);
                cmd.Parameters.AddWithValue("@id", idTerpilih);
                using var r = cmd.ExecuteReader();
                if (!r.Read()) return;

                dtpTanggal.Value = Convert.ToDateTime(r["tanggal"]);
                cbSupplier.SelectedValue = Convert.ToInt32(r["id_supplier"]);
                cbProduk.SelectedValue = Convert.ToInt32(r["id_produk"]);
                cbKaryawan.SelectedValue = r["id_karyawan"] == DBNull.Value ? 0 : Convert.ToInt32(r["id_karyawan"]);
                txtJumlah.Text = Convert.ToInt32(r["jumlah"]).ToString();
                txtHarga.Text = Convert.ToDecimal(r["harga_beli"]).ToString("0", CultureInfo.CurrentCulture);
                txtKeterangan.Text = r["keterangan"] == DBNull.Value ? "" : r["keterangan"].ToString();
                TampilStok();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex);
                MessageBox.Show("Gagal membaca data. Coba lagi.");
            }
        }

        private void Bersihkan()
        {
            idTerpilih = 0;
            dtpTanggal.Value = DateTime.Today;
            if (cbSupplier.Items.Count > 0) cbSupplier.SelectedIndex = 0;
            if (cbProduk.Items.Count > 0) cbProduk.SelectedIndex = 0;
            if (cbKaryawan.Items.Count > 0) cbKaryawan.SelectedIndex = 0;
            txtJumlah.Clear();
            txtHarga.Clear();
            txtKeterangan.Clear();
            lblTotal.Text = "";
            TampilStok();
            grid.ClearSelection();
            txtJumlah.Focus();
        }

        // ================= validasi =================
        private bool Validasi(out int jumlah, out decimal harga)
        {
            jumlah = 0;
            harga = 0;

            if (cbSupplier.SelectedValue is not int)
            {
                MessageBox.Show("Supplier belum ada. Isi dulu lewat menu Supplier.");
                return false;
            }
            if (cbProduk.SelectedValue is not int)
            {
                MessageBox.Show("Produk belum ada. Isi dulu lewat menu Produk.");
                return false;
            }
            if (!int.TryParse(txtJumlah.Text.Trim(), NumberStyles.None, CultureInfo.CurrentCulture, out jumlah)
                || jumlah <= 0)
            {
                MessageBox.Show("Jumlah beli harus berupa angka bulat lebih dari 0.");
                return false;
            }
            if (!decimal.TryParse(txtHarga.Text.Trim(), NumberStyles.None, CultureInfo.CurrentCulture, out harga)
                || harga <= 0)
            {
                MessageBox.Show("Harga beli harus berupa angka bulat lebih dari 0 (tanpa titik atau koma).");
                return false;
            }
            return true;
        }

        private object IdKaryawanOrNull()
        {
            int id = cbKaryawan.SelectedValue is int v ? v : 0;
            return id == 0 ? DBNull.Value : id;
        }

        private object KeteranganOrNull()
        {
            return string.IsNullOrWhiteSpace(txtKeterangan.Text) ? DBNull.Value : txtKeterangan.Text.Trim();
        }

        // ================= penyesuaian stok =================
        // delta positif = stok bertambah, negatif = stok berkurang (ditolak kalau jadi minus)
        private static void UbahStok(MySqlConnection conn, MySqlTransaction tx, int idProduk, int delta)
        {
            if (delta == 0) return;

            if (delta > 0)
            {
                using var cmd = new MySqlCommand(
                    "INSERT INTO stock (id_produk, jumlah) VALUES (@p, @d) " +
                    "ON DUPLICATE KEY UPDATE jumlah = jumlah + @d", conn, tx);
                cmd.Parameters.AddWithValue("@p", idProduk);
                cmd.Parameters.AddWithValue("@d", delta);
                cmd.ExecuteNonQuery();
            }
            else
            {
                using var cmd = new MySqlCommand(
                    "UPDATE stock SET jumlah = jumlah + @d WHERE id_produk=@p AND jumlah >= @min", conn, tx);
                cmd.Parameters.AddWithValue("@p", idProduk);
                cmd.Parameters.AddWithValue("@d", delta);
                cmd.Parameters.AddWithValue("@min", -delta);
                if (cmd.ExecuteNonQuery() == 0)
                    throw new StokException(
                        "Perubahan ini tidak bisa dilakukan karena stok produk akan menjadi minus " +
                        "(sebagian barang sudah terjual).");
            }
        }

        // ================= tambah =================
        private void BtnTambah_Click(object? sender, EventArgs e)
        {
            if (!Validasi(out int jumlah, out decimal harga)) return;

            try
            {
                using var conn = Koneksi.GetConnection();
                conn.Open();
                using var tx = conn.BeginTransaction();

                using (var cmd = new MySqlCommand(
                    "INSERT INTO purchase_stock (id_supplier, id_produk, id_karyawan, tanggal, jumlah, harga_beli, keterangan) " +
                    "VALUES (@sup, @prod, @kar, @tgl, @jml, @hrg, @ket)", conn, tx))
                {
                    cmd.Parameters.AddWithValue("@sup", (int)cbSupplier.SelectedValue!);
                    cmd.Parameters.AddWithValue("@prod", (int)cbProduk.SelectedValue!);
                    cmd.Parameters.AddWithValue("@kar", IdKaryawanOrNull());
                    cmd.Parameters.AddWithValue("@tgl", dtpTanggal.Value.Date);
                    cmd.Parameters.AddWithValue("@jml", jumlah);
                    cmd.Parameters.AddWithValue("@hrg", harga);
                    cmd.Parameters.AddWithValue("@ket", KeteranganOrNull());
                    cmd.ExecuteNonQuery();
                }

                UbahStok(conn, tx, (int)cbProduk.SelectedValue!, jumlah);
                tx.Commit();

                MessageBox.Show("Pembelian berhasil dicatat. Stok produk bertambah.");
                Bersihkan();
                MuatData();
            }
            catch (MySqlException ex) when (ex.Number == 3819)
            {
                MessageBox.Show("Data ditolak database: ada nilai yang tidak sesuai aturan.");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex);
                MessageBox.Show("Gagal mencatat pembelian. Coba lagi.");
            }
        }

        // ================= ubah =================
        private void BtnUbah_Click(object? sender, EventArgs e)
        {
            if (idTerpilih == 0)
            {
                MessageBox.Show("Klik dulu pembelian yang mau diubah di tabel.");
                return;
            }
            if (!Validasi(out int jumlahBaru, out decimal harga)) return;

            int produkBaru = (int)cbProduk.SelectedValue!;

            try
            {
                using var conn = Koneksi.GetConnection();
                conn.Open();
                using var tx = conn.BeginTransaction();

                int produkLama = 0, jumlahLama = 0;
                using (var cek = new MySqlCommand(
                    "SELECT id_produk, jumlah FROM purchase_stock WHERE id_purchase=@id FOR UPDATE", conn, tx))
                {
                    cek.Parameters.AddWithValue("@id", idTerpilih);
                    using var r = cek.ExecuteReader();
                    if (!r.Read())
                    {
                        MessageBox.Show("Data pembelian tidak ditemukan.");
                        return;
                    }
                    produkLama = r.GetInt32(0);
                    jumlahLama = r.GetInt32(1);
                }

                // sesuaikan stok: produk sama = selisihnya saja, produk beda = batalkan yang lama, tambah yang baru
                if (produkLama == produkBaru)
                {
                    UbahStok(conn, tx, produkBaru, jumlahBaru - jumlahLama);
                }
                else
                {
                    UbahStok(conn, tx, produkLama, -jumlahLama);
                    UbahStok(conn, tx, produkBaru, jumlahBaru);
                }

                using (var cmd = new MySqlCommand(
                    "UPDATE purchase_stock SET id_supplier=@sup, id_produk=@prod, id_karyawan=@kar, " +
                    "tanggal=@tgl, jumlah=@jml, harga_beli=@hrg, keterangan=@ket WHERE id_purchase=@id", conn, tx))
                {
                    cmd.Parameters.AddWithValue("@sup", (int)cbSupplier.SelectedValue!);
                    cmd.Parameters.AddWithValue("@prod", produkBaru);
                    cmd.Parameters.AddWithValue("@kar", IdKaryawanOrNull());
                    cmd.Parameters.AddWithValue("@tgl", dtpTanggal.Value.Date);
                    cmd.Parameters.AddWithValue("@jml", jumlahBaru);
                    cmd.Parameters.AddWithValue("@hrg", harga);
                    cmd.Parameters.AddWithValue("@ket", KeteranganOrNull());
                    cmd.Parameters.AddWithValue("@id", idTerpilih);
                    cmd.ExecuteNonQuery();
                }

                tx.Commit();

                MessageBox.Show("Pembelian berhasil diubah. Stok sudah disesuaikan.");
                Bersihkan();
                MuatData();
            }
            catch (StokException ex)
            {
                MessageBox.Show(ex.Message);
            }
            catch (MySqlException ex) when (ex.Number == 3819)
            {
                MessageBox.Show("Data ditolak database: ada nilai yang tidak sesuai aturan.");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex);
                MessageBox.Show("Gagal mengubah pembelian. Coba lagi.");
            }
        }

        // ================= hapus =================
        private void BtnHapus_Click(object? sender, EventArgs e)
        {
            if (idTerpilih == 0)
            {
                MessageBox.Show("Klik dulu pembelian yang mau dihapus di tabel.");
                return;
            }

            var yakin = MessageBox.Show(
                "Yakin hapus pembelian ini?\nStok produk akan dikurangi sesuai jumlah pembelian.",
                "Konfirmasi", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (yakin != DialogResult.Yes) return;

            try
            {
                using var conn = Koneksi.GetConnection();
                conn.Open();
                using var tx = conn.BeginTransaction();

                int produk = 0, jumlah = 0;
                using (var cek = new MySqlCommand(
                    "SELECT id_produk, jumlah FROM purchase_stock WHERE id_purchase=@id FOR UPDATE", conn, tx))
                {
                    cek.Parameters.AddWithValue("@id", idTerpilih);
                    using var r = cek.ExecuteReader();
                    if (!r.Read())
                    {
                        MessageBox.Show("Data pembelian tidak ditemukan.");
                        return;
                    }
                    produk = r.GetInt32(0);
                    jumlah = r.GetInt32(1);
                }

                UbahStok(conn, tx, produk, -jumlah);

                using (var cmd = new MySqlCommand(
                    "DELETE FROM purchase_stock WHERE id_purchase=@id", conn, tx))
                {
                    cmd.Parameters.AddWithValue("@id", idTerpilih);
                    cmd.ExecuteNonQuery();
                }

                tx.Commit();

                MessageBox.Show("Pembelian berhasil dihapus. Stok sudah dikurangi.");
                Bersihkan();
                MuatData();
            }
            catch (StokException ex)
            {
                MessageBox.Show(ex.Message);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex);
                MessageBox.Show("Gagal menghapus pembelian. Coba lagi.");
            }
        }
    }
}