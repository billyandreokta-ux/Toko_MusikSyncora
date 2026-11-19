using System.Data;
using System.Globalization;
using MySql.Data.MySqlClient;
using TokoMusik.Data;

namespace TokoMusik.Forms
{
    public class FormProduk : Form
    {
        private readonly TextBox txtKode = new();
        private readonly TextBox txtNama = new();
        private readonly TextBox txtHarga = new();
        private readonly TextBox txtDeskripsi = new();
        private readonly TextBox txtWarna = new();
        private readonly TextBox txtBahan = new();
        private readonly TextBox txtUkuran = new();
        private readonly TextBox txtBerat = new();
        private readonly TextBox txtSpesifikasi = new();
        private readonly TextBox txtCari = new();
        private readonly ComboBox cbCategory = new();
        private readonly ComboBox cbMerk = new();
        private readonly ComboBox cbStatus = new();
        private readonly PictureBox pic = new();
        private readonly DataGridView grid = new();

        private int idTerpilih = 0;
        private string fotoLama = "";       // nama file foto yang tersimpan di database
        private string? fotoSumber = null;  // foto baru yang dipilih tapi belum disimpan
        private bool hapusFoto = false;

        public FormProduk()
        {
            Text = "Data Produk";
            ClientSize = new Size(1020, 700);
            MinimumSize = new Size(900, 600);
            StartPosition = FormStartPosition.CenterParent;
            Font = new Font("Segoe UI", 10F);
            AutoScroll = true;

            // ---------- batas panjang input (sama dengan kolom database) ----------
            txtKode.MaxLength = 30;
            txtNama.MaxLength = 150;
            txtHarga.MaxLength = 10;     // maksimal 9.999.999.999
            txtWarna.MaxLength = 50;
            txtBahan.MaxLength = 100;
            txtUkuran.MaxLength = 50;
            txtBerat.MaxLength = 7;

            // ---------- kolom input ----------
            TambahInput("Kode Produk *", txtKode, 20, 10, 300);
            TambahInput("Nama Produk *", txtNama, 340, 10, 310);
            TambahInput("Category *", cbCategory, 20, 64, 300);
            TambahInput("Merk *", cbMerk, 340, 64, 310);
            TambahInput("Harga Jual (Rp) *", txtHarga, 20, 118, 300);
            TambahInput("Status", cbStatus, 340, 118, 310);
            TambahInput("Warna", txtWarna, 20, 172, 300);
            TambahInput("Bahan", txtBahan, 340, 172, 310);
            TambahInput("Ukuran", txtUkuran, 20, 226, 300);
            TambahInput("Berat (gram)", txtBerat, 340, 226, 310);
            TambahInput("Deskripsi", txtDeskripsi, 20, 280, 630);
            TambahInput("Spesifikasi", txtSpesifikasi, 20, 334, 630);

            cbStatus.DropDownStyle = ComboBoxStyle.DropDownList;
            cbStatus.Items.AddRange(new object[] { "aktif", "nonaktif" });

            // ---------- foto ----------
            Controls.Add(new Label { Text = "Foto Produk", Bounds = new Rectangle(680, 10, 300, 20) });
            pic.Bounds = new Rectangle(680, 32, 320, 270);
            pic.SizeMode = PictureBoxSizeMode.Zoom;
            pic.BorderStyle = BorderStyle.FixedSingle;
            pic.BackColor = Color.White;
            Controls.Add(pic);

            var btnPilih = new Button { Text = "Pilih Foto", Bounds = new Rectangle(680, 310, 150, 34) };
            btnPilih.Click += BtnPilihFoto_Click;
            Controls.Add(btnPilih);

            var btnHapusFoto = new Button { Text = "Hapus Foto", Bounds = new Rectangle(840, 310, 150, 34) };
            btnHapusFoto.Click += (s, e) =>
            {
                fotoSumber = null;
                hapusFoto = true;
                TampilFoto(null);
            };
            Controls.Add(btnHapusFoto);

            // ---------- tombol aksi ----------
            Controls.Add(BuatTombol("Tambah", 20, 392, BtnTambah_Click));
            Controls.Add(BuatTombol("Ubah", 140, 392, BtnUbah_Click));
            Controls.Add(BuatTombol("Hapus", 260, 392, BtnHapus_Click));
            Controls.Add(BuatTombol("Bersihkan", 380, 392, (s, e) => Bersihkan()));

            // ---------- pencarian ----------
            Controls.Add(new Label { Text = "Cari:", Bounds = new Rectangle(20, 443, 50, 22) });
            txtCari.Bounds = new Rectangle(70, 440, 300, 28);
            txtCari.TextChanged += (s, e) => MuatData();
            Controls.Add(txtCari);

            // ---------- tabel ----------
            grid.Bounds = new Rectangle(20, 478, 980, 210);
            grid.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            grid.ReadOnly = true;
            grid.AllowUserToAddRows = false;
            grid.AllowUserToDeleteRows = false;
            grid.MultiSelect = false;
            grid.RowHeadersVisible = false;
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            grid.BackgroundColor = Color.White;   // area kosong di bawah baris jadi putih
            grid.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.True;
            grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            grid.CellClick += Grid_CellClick;
            Controls.Add(grid);

            // ---------- isi pilihan dan data awal ----------
            try
            {
                IsiCombo(cbCategory, "SELECT id_category, nama_category FROM category ORDER BY nama_category");
                IsiCombo(cbMerk, "SELECT id_merk, nama_merk FROM merk ORDER BY nama_merk");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex);
                MessageBox.Show("Gagal memuat Category atau Merk. Coba lagi.");
            }

            Bersihkan();
            Load += (s, e) => MuatData();
            FormClosed += (s, e) => pic.Image?.Dispose();
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

        private static void IsiCombo(ComboBox cb, string sql)
        {
            var dt = new DataTable();
            dt.Columns.Add("Id", typeof(int));
            dt.Columns.Add("Nama", typeof(string));

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

        private void TampilFoto(Image? img)
        {
            pic.Image?.Dispose();
            pic.Image = img;
        }

        private static string S(object val) => val == DBNull.Value ? "" : val.ToString() ?? "";

        private static object Nil(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return DBNull.Value;
            return s.Trim();
        }

        // ================= tampil data =================
        private void MuatData()
        {
            try
            {
                using var conn = Koneksi.GetConnection();
                conn.Open();
                using var da = new MySqlDataAdapter(
                    "SELECT p.id_produk AS ID, p.kode_produk AS Kode, p.nama_produk AS Nama, " +
                    "c.nama_category AS Category, m.nama_merk AS Merk, p.harga_jual AS Harga, " +
                    "IFNULL(s.jumlah, 0) AS Stok, p.status AS Status " +
                    "FROM produk p " +
                    "JOIN category c ON c.id_category = p.id_category " +
                    "JOIN merk m ON m.id_merk = p.id_merk " +
                    "LEFT JOIN stock s ON s.id_produk = p.id_produk " +
                    "WHERE p.nama_produk LIKE @cari OR p.kode_produk LIKE @cari " +
                    "ORDER BY p.id_produk DESC", conn);
                da.SelectCommand.Parameters.AddWithValue("@cari", "%" + txtCari.Text.Trim() + "%");
                var dt = new DataTable();
                da.Fill(dt);
                grid.DataSource = dt;

                if (grid.Columns.Contains("ID")) grid.Columns["ID"]!.FillWeight = 25;
                if (grid.Columns.Contains("Stok")) grid.Columns["Stok"]!.FillWeight = 30;
                if (grid.Columns.Contains("Harga")) grid.Columns["Harga"]!.DefaultCellStyle.Format = "N0";
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
                    "SELECT p.*, d.warna, d.bahan, d.ukuran, d.berat_gram, d.spesifikasi " +
                    "FROM produk p LEFT JOIN detail_produk d ON d.id_produk = p.id_produk " +
                    "WHERE p.id_produk = @id", conn);
                cmd.Parameters.AddWithValue("@id", idTerpilih);
                using var r = cmd.ExecuteReader();
                if (!r.Read()) return;

                txtKode.Text = S(r["kode_produk"]);
                txtNama.Text = S(r["nama_produk"]);
                txtHarga.Text = Convert.ToDecimal(r["harga_jual"]).ToString("0", CultureInfo.CurrentCulture);
                txtDeskripsi.Text = S(r["deskripsi"]);
                txtWarna.Text = S(r["warna"]);
                txtBahan.Text = S(r["bahan"]);
                txtUkuran.Text = S(r["ukuran"]);
                txtBerat.Text = S(r["berat_gram"]);
                txtSpesifikasi.Text = S(r["spesifikasi"]);

                cbCategory.SelectedValue = Convert.ToInt32(r["id_category"]);
                cbMerk.SelectedValue = Convert.ToInt32(r["id_merk"]);
                int idx = cbStatus.Items.IndexOf(S(r["status"]));
                if (idx >= 0) cbStatus.SelectedIndex = idx;

                fotoLama = S(r["foto"]);
                fotoSumber = null;
                hapusFoto = false;
                TampilFoto(FotoHelper.Muat(fotoLama));
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
            fotoLama = "";
            fotoSumber = null;
            hapusFoto = false;

            txtKode.Clear();
            txtNama.Clear();
            txtHarga.Clear();
            txtDeskripsi.Clear();
            txtWarna.Clear();
            txtBahan.Clear();
            txtUkuran.Clear();
            txtBerat.Clear();
            txtSpesifikasi.Clear();

            if (cbCategory.Items.Count > 0) cbCategory.SelectedIndex = 0;
            if (cbMerk.Items.Count > 0) cbMerk.SelectedIndex = 0;
            if (cbStatus.Items.Count > 0) cbStatus.SelectedIndex = 0;

            TampilFoto(null);
            grid.ClearSelection();
            txtKode.Focus();
        }

        // ================= foto =================
        private void BtnPilihFoto_Click(object? sender, EventArgs e)
        {
            using var dlg = new OpenFileDialog
            {
                Title = "Pilih foto produk",
                Filter = "Gambar|*.jpg;*.jpeg;*.png;*.gif;*.bmp"
            };
            if (dlg.ShowDialog() != DialogResult.OK) return;

            var img = FotoHelper.MuatDariPath(dlg.FileName);
            if (img == null)
            {
                MessageBox.Show("File gambar tidak bisa dibuka.");
                return;
            }

            fotoSumber = dlg.FileName;
            hapusFoto = false;
            TampilFoto(img);
        }

        // ================= validasi =================
        private bool Validasi(out decimal harga, out object berat)
        {
            harga = 0;
            berat = DBNull.Value;

            if (string.IsNullOrWhiteSpace(txtKode.Text))
            {
                MessageBox.Show("Kode produk wajib diisi.");
                return false;
            }
            if (string.IsNullOrWhiteSpace(txtNama.Text))
            {
                MessageBox.Show("Nama produk wajib diisi.");
                return false;
            }
            if (cbCategory.SelectedValue == null)
            {
                MessageBox.Show("Category belum ada. Isi dulu lewat menu Category.");
                return false;
            }
            if (cbMerk.SelectedValue == null)
            {
                MessageBox.Show("Merk belum ada. Isi dulu lewat menu Merk.");
                return false;
            }
            // NumberStyles.None: hanya digit, jadi "1.500" tidak salah dibaca sebagai 1,5
            if (!decimal.TryParse(txtHarga.Text.Trim(), NumberStyles.None,
                    CultureInfo.CurrentCulture, out harga) || harga <= 0)
            {
                MessageBox.Show("Harga harus berupa angka bulat lebih dari 0 (tanpa titik atau koma).");
                return false;
            }
            if (txtBerat.Text.Trim() != "")
            {
                if (!int.TryParse(txtBerat.Text.Trim(), NumberStyles.None,
                        CultureInfo.CurrentCulture, out int b))
                {
                    MessageBox.Show("Berat harus berupa angka bulat (gram).");
                    return false;
                }
                berat = b;
            }
            return true;
        }

        // ================= tambah =================
        private void BtnTambah_Click(object? sender, EventArgs e)
        {
            if (!Validasi(out decimal harga, out object berat)) return;

            string? fotoBaru = null;
            try
            {
                if (fotoSumber != null) fotoBaru = FotoHelper.Simpan(fotoSumber);

                using var conn = Koneksi.GetConnection();
                conn.Open();
                using var tx = conn.BeginTransaction();

                using var cmd = new MySqlCommand(
                    "INSERT INTO produk (id_category, id_merk, kode_produk, nama_produk, harga_jual, deskripsi, foto, status) " +
                    "VALUES (@cat, @merk, @kode, @nama, @harga, @desk, @foto, @status)", conn, tx);
                cmd.Parameters.AddWithValue("@cat", Convert.ToInt32(cbCategory.SelectedValue));
                cmd.Parameters.AddWithValue("@merk", Convert.ToInt32(cbMerk.SelectedValue));
                cmd.Parameters.AddWithValue("@kode", txtKode.Text.Trim());
                cmd.Parameters.AddWithValue("@nama", txtNama.Text.Trim());
                cmd.Parameters.AddWithValue("@harga", harga);
                cmd.Parameters.AddWithValue("@desk", Nil(txtDeskripsi.Text));
                cmd.Parameters.AddWithValue("@foto", fotoBaru == null ? DBNull.Value : fotoBaru);
                cmd.Parameters.AddWithValue("@status", cbStatus.SelectedItem?.ToString() ?? "aktif");
                cmd.ExecuteNonQuery();
                long idBaru = cmd.LastInsertedId;

                using var cmdDetail = new MySqlCommand(
                    "INSERT INTO detail_produk (id_produk, warna, bahan, ukuran, berat_gram, spesifikasi) " +
                    "VALUES (@id, @w, @b, @u, @br, @sp)", conn, tx);
                cmdDetail.Parameters.AddWithValue("@id", idBaru);
                cmdDetail.Parameters.AddWithValue("@w", Nil(txtWarna.Text));
                cmdDetail.Parameters.AddWithValue("@b", Nil(txtBahan.Text));
                cmdDetail.Parameters.AddWithValue("@u", Nil(txtUkuran.Text));
                cmdDetail.Parameters.AddWithValue("@br", berat);
                cmdDetail.Parameters.AddWithValue("@sp", Nil(txtSpesifikasi.Text));
                cmdDetail.ExecuteNonQuery();

                using var cmdStock = new MySqlCommand(
                    "INSERT INTO stock (id_produk, jumlah) VALUES (@id, 0)", conn, tx);
                cmdStock.Parameters.AddWithValue("@id", idBaru);
                cmdStock.ExecuteNonQuery();

                tx.Commit();

                MessageBox.Show("Produk berhasil ditambahkan.");
                Bersihkan();
                MuatData();
            }
            catch (MySqlException ex) when (ex.Number == 1062)
            {
                FotoHelper.Hapus(fotoBaru);
                MessageBox.Show("Kode produk sudah dipakai produk lain.");
            }
            catch (InvalidOperationException ex)
            {
                // pesan dari FotoHelper (format foto tidak didukung)
                FotoHelper.Hapus(fotoBaru);
                MessageBox.Show(ex.Message);
            }
            catch (Exception ex)
            {
                FotoHelper.Hapus(fotoBaru);
                System.Diagnostics.Debug.WriteLine(ex);
                MessageBox.Show("Gagal menambah produk. Coba lagi.");
            }
        }

        // ================= ubah =================
        private void BtnUbah_Click(object? sender, EventArgs e)
        {
            if (idTerpilih == 0)
            {
                MessageBox.Show("Klik dulu produk yang mau diubah di tabel.");
                return;
            }
            if (!Validasi(out decimal harga, out object berat)) return;

            string? fotoBaru = null;
            string fotoFinal = fotoLama;
            try
            {
                if (fotoSumber != null)
                {
                    fotoBaru = FotoHelper.Simpan(fotoSumber);
                    fotoFinal = fotoBaru;
                }
                else if (hapusFoto)
                {
                    fotoFinal = "";
                }

                using var conn = Koneksi.GetConnection();
                conn.Open();
                using var tx = conn.BeginTransaction();

                using var cmd = new MySqlCommand(
                    "UPDATE produk SET id_category=@cat, id_merk=@merk, kode_produk=@kode, nama_produk=@nama, " +
                    "harga_jual=@harga, deskripsi=@desk, foto=@foto, status=@status WHERE id_produk=@id", conn, tx);
                cmd.Parameters.AddWithValue("@cat", Convert.ToInt32(cbCategory.SelectedValue));
                cmd.Parameters.AddWithValue("@merk", Convert.ToInt32(cbMerk.SelectedValue));
                cmd.Parameters.AddWithValue("@kode", txtKode.Text.Trim());
                cmd.Parameters.AddWithValue("@nama", txtNama.Text.Trim());
                cmd.Parameters.AddWithValue("@harga", harga);
                cmd.Parameters.AddWithValue("@desk", Nil(txtDeskripsi.Text));
                cmd.Parameters.AddWithValue("@foto", Nil(fotoFinal));
                cmd.Parameters.AddWithValue("@status", cbStatus.SelectedItem?.ToString() ?? "aktif");
                cmd.Parameters.AddWithValue("@id", idTerpilih);
                cmd.ExecuteNonQuery();

                using var cmdDetail = new MySqlCommand(
                    "INSERT INTO detail_produk (id_produk, warna, bahan, ukuran, berat_gram, spesifikasi) " +
                    "VALUES (@id, @w, @b, @u, @br, @sp) " +
                    "ON DUPLICATE KEY UPDATE warna=@w, bahan=@b, ukuran=@u, berat_gram=@br, spesifikasi=@sp", conn, tx);
                cmdDetail.Parameters.AddWithValue("@id", idTerpilih);
                cmdDetail.Parameters.AddWithValue("@w", Nil(txtWarna.Text));
                cmdDetail.Parameters.AddWithValue("@b", Nil(txtBahan.Text));
                cmdDetail.Parameters.AddWithValue("@u", Nil(txtUkuran.Text));
                cmdDetail.Parameters.AddWithValue("@br", berat);
                cmdDetail.Parameters.AddWithValue("@sp", Nil(txtSpesifikasi.Text));
                cmdDetail.ExecuteNonQuery();

                tx.Commit();

                // foto lama dibuang hanya jika sudah diganti atau dihapus
                if (fotoFinal != fotoLama) FotoHelper.Hapus(fotoLama);

                MessageBox.Show("Produk berhasil diubah.");
                Bersihkan();
                MuatData();
            }
            catch (MySqlException ex) when (ex.Number == 1062)
            {
                FotoHelper.Hapus(fotoBaru);
                MessageBox.Show("Kode produk sudah dipakai produk lain.");
            }
            catch (InvalidOperationException ex)
            {
                // pesan dari FotoHelper (format foto tidak didukung)
                FotoHelper.Hapus(fotoBaru);
                MessageBox.Show(ex.Message);
            }
            catch (Exception ex)
            {
                FotoHelper.Hapus(fotoBaru);
                System.Diagnostics.Debug.WriteLine(ex);
                MessageBox.Show("Gagal mengubah produk. Coba lagi.");
            }
        }

        // ================= hapus =================
        private void BtnHapus_Click(object? sender, EventArgs e)
        {
            if (idTerpilih == 0)
            {
                MessageBox.Show("Klik dulu produk yang mau dihapus di tabel.");
                return;
            }

            var yakin = MessageBox.Show("Yakin hapus produk ini?", "Konfirmasi",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (yakin != DialogResult.Yes) return;

            try
            {
                using var conn = Koneksi.GetConnection();
                conn.Open();
                using var cmd = new MySqlCommand("DELETE FROM produk WHERE id_produk=@id", conn);
                cmd.Parameters.AddWithValue("@id", idTerpilih);
                cmd.ExecuteNonQuery();

                FotoHelper.Hapus(fotoLama);

                MessageBox.Show("Produk berhasil dihapus.");
                Bersihkan();
                MuatData();
            }
            catch (MySqlException ex) when (ex.Number == 1451)
            {
                MessageBox.Show("Produk ini tidak bisa dihapus karena sudah dipakai di pembelian stok atau pesanan.\n" +
                                "Ubah statusnya menjadi 'nonaktif' saja supaya tidak tampil di katalog.");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex);
                MessageBox.Show("Gagal menghapus produk. Coba lagi.");
            }
        }
    }
}