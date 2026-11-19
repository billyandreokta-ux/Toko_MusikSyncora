using System.Data;
using MySql.Data.MySqlClient;
using TokoMusik.Data;

namespace TokoMusik.Forms
{
    public class FormAlamatSaya : Form
    {
        private readonly TextBox txtLabel = new();
        private readonly TextBox txtPenerima = new();
        private readonly TextBox txtTelepon = new();
        private readonly TextBox txtKota = new();
        private readonly TextBox txtProvinsi = new();
        private readonly TextBox txtKodePos = new();
        private readonly TextBox txtAlamat = new();
        private readonly CheckBox chkUtama = new();
        private readonly DataGridView grid = new();

        private int idCustomer = 0;
        private int idTerpilih = 0;

        public FormAlamatSaya()
        {
            Text = "Alamat Saya";
            ClientSize = new Size(900, 600);
            MinimumSize = new Size(800, 520);
            StartPosition = FormStartPosition.CenterParent;
            Font = new Font("Segoe UI", 10F);

            try { idCustomer = PesananService.GetIdCustomer() ?? 0; }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }

            txtLabel.MaxLength = 50;
            txtPenerima.MaxLength = 100;
            txtTelepon.MaxLength = 20;
            txtKota.MaxLength = 100;
            txtProvinsi.MaxLength = 100;
            txtKodePos.MaxLength = 10;

            TambahInput("Label Alamat (mis. Rumah, Kantor)", txtLabel, 20, 10, 410);
            TambahInput("Nama Penerima *", txtPenerima, 450, 10, 430);
            TambahInput("Telepon", txtTelepon, 20, 64, 410);
            TambahInput("Kota", txtKota, 450, 64, 430);
            TambahInput("Provinsi", txtProvinsi, 20, 118, 410);
            TambahInput("Kode Pos", txtKodePos, 450, 118, 430);
            TambahInput("Alamat Lengkap *", txtAlamat, 20, 172, 860);

            chkUtama.Text = "Jadikan alamat utama";
            chkUtama.Bounds = new Rectangle(20, 230, 300, 26);
            Controls.Add(chkUtama);

            Controls.Add(BuatTombol("Tambah", 20, 266, BtnTambah_Click));
            Controls.Add(BuatTombol("Ubah", 140, 266, BtnUbah_Click));
            Controls.Add(BuatTombol("Hapus", 260, 266, BtnHapus_Click));
            Controls.Add(BuatTombol("Bersihkan", 380, 266, (s, e) => Bersihkan()));

            grid.Bounds = new Rectangle(20, 316, 860, 264);
            grid.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            grid.ReadOnly = true;
            grid.AllowUserToAddRows = false;
            grid.AllowUserToDeleteRows = false;
            grid.MultiSelect = false;
            grid.RowHeadersVisible = false;
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            grid.BackgroundColor = Color.White;
            grid.CellClick += Grid_CellClick;
            grid.DataBindingComplete += (s, e) => grid.ClearSelection();
            Controls.Add(grid);

            Bersihkan();
            Load += (s, e) =>
            {
                if (idCustomer == 0)
                {
                    MessageBox.Show("Data customer untuk akun ini tidak ditemukan. Hubungi admin.");
                    BeginInvoke(new Action(Close));
                    return;
                }
                MuatData();
            };
        }

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

        private static string S(object v) => v == DBNull.Value ? "" : v.ToString() ?? "";

        private static object Nil(string s) =>
            string.IsNullOrWhiteSpace(s) ? DBNull.Value : s.Trim();

        private void MuatData()
        {
            if (idCustomer == 0) return;
            try
            {
                using var conn = Koneksi.GetConnection();
                conn.Open();
                using var da = new MySqlDataAdapter(
                    "SELECT id_alamat AS ID, label_alamat AS Label, penerima AS Penerima, telepon AS Telepon, " +
                    "alamat_lengkap AS Alamat, kota AS Kota, IF(is_utama = 1, 'Ya', '') AS Utama " +
                    "FROM alamat_customer WHERE id_customer=@c ORDER BY is_utama DESC, id_alamat DESC", conn);
                da.SelectCommand.Parameters.AddWithValue("@c", idCustomer);
                var dt = new DataTable();
                da.Fill(dt);
                grid.DataSource = dt;
                if (grid.Columns.Contains("ID")) grid.Columns["ID"]!.FillWeight = 20;
                if (grid.Columns.Contains("Utama")) grid.Columns["Utama"]!.FillWeight = 25;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex);
                MessageBox.Show("Gagal memuat alamat. Coba lagi.");
            }
        }

        private void Grid_CellClick(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;
            idTerpilih = Convert.ToInt32(grid.Rows[e.RowIndex].Cells["ID"].Value);

            try
            {
                using var conn = Koneksi.GetConnection();
                conn.Open();
                using var cmd = new MySqlCommand(
                    "SELECT * FROM alamat_customer WHERE id_alamat=@id AND id_customer=@c", conn);
                cmd.Parameters.AddWithValue("@id", idTerpilih);
                cmd.Parameters.AddWithValue("@c", idCustomer);
                using var r = cmd.ExecuteReader();
                if (!r.Read()) return;

                txtLabel.Text = S(r["label_alamat"]);
                txtPenerima.Text = S(r["penerima"]);
                txtTelepon.Text = S(r["telepon"]);
                txtKota.Text = S(r["kota"]);
                txtProvinsi.Text = S(r["provinsi"]);
                txtKodePos.Text = S(r["kode_pos"]);
                txtAlamat.Text = S(r["alamat_lengkap"]);
                chkUtama.Checked = Convert.ToInt32(r["is_utama"]) == 1;
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
            txtLabel.Text = "Rumah";
            txtPenerima.Clear();
            txtTelepon.Clear();
            txtKota.Clear();
            txtProvinsi.Clear();
            txtKodePos.Clear();
            txtAlamat.Clear();
            chkUtama.Checked = false;
            grid.ClearSelection();
            txtPenerima.Focus();
        }

        private bool Validasi()
        {
            if (string.IsNullOrWhiteSpace(txtPenerima.Text))
            {
                MessageBox.Show("Nama penerima wajib diisi.");
                return false;
            }
            if (string.IsNullOrWhiteSpace(txtAlamat.Text))
            {
                MessageBox.Show("Alamat lengkap wajib diisi.");
                return false;
            }
            return true;
        }

        private void KosongkanAlamatUtama(MySqlConnection conn, MySqlTransaction tx)
        {
            using var cmd = new MySqlCommand(
                "UPDATE alamat_customer SET is_utama=0 WHERE id_customer=@c", conn, tx);
            cmd.Parameters.AddWithValue("@c", idCustomer);
            cmd.ExecuteNonQuery();
        }

        private void BtnTambah_Click(object? sender, EventArgs e)
        {
            if (!Validasi()) return;

            try
            {
                using var conn = Koneksi.GetConnection();
                conn.Open();
                using var tx = conn.BeginTransaction();

                bool utama = chkUtama.Checked;

                // alamat pertama otomatis menjadi alamat utama
                using (var hitung = new MySqlCommand(
                    "SELECT COUNT(*) FROM alamat_customer WHERE id_customer=@c", conn, tx))
                {
                    hitung.Parameters.AddWithValue("@c", idCustomer);
                    if (Convert.ToInt32(hitung.ExecuteScalar()) == 0) utama = true;
                }

                if (utama) KosongkanAlamatUtama(conn, tx);

                using (var cmd = new MySqlCommand(
                    "INSERT INTO alamat_customer (id_customer, label_alamat, penerima, telepon, alamat_lengkap, kota, provinsi, kode_pos, is_utama) " +
                    "VALUES (@c, @l, @p, @t, @a, @k, @pv, @kp, @u)", conn, tx))
                {
                    cmd.Parameters.AddWithValue("@c", idCustomer);
                    cmd.Parameters.AddWithValue("@l", Nil(txtLabel.Text));
                    cmd.Parameters.AddWithValue("@p", txtPenerima.Text.Trim());
                    cmd.Parameters.AddWithValue("@t", Nil(txtTelepon.Text));
                    cmd.Parameters.AddWithValue("@a", txtAlamat.Text.Trim());
                    cmd.Parameters.AddWithValue("@k", Nil(txtKota.Text));
                    cmd.Parameters.AddWithValue("@pv", Nil(txtProvinsi.Text));
                    cmd.Parameters.AddWithValue("@kp", Nil(txtKodePos.Text));
                    cmd.Parameters.AddWithValue("@u", utama ? 1 : 0);
                    cmd.ExecuteNonQuery();
                }

                tx.Commit();
                MessageBox.Show("Alamat berhasil ditambahkan.");
                Bersihkan();
                MuatData();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex);
                MessageBox.Show("Gagal menambah alamat. Coba lagi.");
            }
        }

        private void BtnUbah_Click(object? sender, EventArgs e)
        {
            if (idTerpilih == 0)
            {
                MessageBox.Show("Klik dulu alamat yang mau diubah di tabel.");
                return;
            }
            if (!Validasi()) return;

            try
            {
                using var conn = Koneksi.GetConnection();
                conn.Open();
                using var tx = conn.BeginTransaction();

                if (chkUtama.Checked) KosongkanAlamatUtama(conn, tx);

                using (var cmd = new MySqlCommand(
                    "UPDATE alamat_customer SET label_alamat=@l, penerima=@p, telepon=@t, alamat_lengkap=@a, " +
                    "kota=@k, provinsi=@pv, kode_pos=@kp, is_utama=@u " +
                    "WHERE id_alamat=@id AND id_customer=@c", conn, tx))
                {
                    cmd.Parameters.AddWithValue("@l", Nil(txtLabel.Text));
                    cmd.Parameters.AddWithValue("@p", txtPenerima.Text.Trim());
                    cmd.Parameters.AddWithValue("@t", Nil(txtTelepon.Text));
                    cmd.Parameters.AddWithValue("@a", txtAlamat.Text.Trim());
                    cmd.Parameters.AddWithValue("@k", Nil(txtKota.Text));
                    cmd.Parameters.AddWithValue("@pv", Nil(txtProvinsi.Text));
                    cmd.Parameters.AddWithValue("@kp", Nil(txtKodePos.Text));
                    cmd.Parameters.AddWithValue("@u", chkUtama.Checked ? 1 : 0);
                    cmd.Parameters.AddWithValue("@id", idTerpilih);
                    cmd.Parameters.AddWithValue("@c", idCustomer);
                    cmd.ExecuteNonQuery();
                }

                tx.Commit();
                MessageBox.Show("Alamat berhasil diubah.");
                Bersihkan();
                MuatData();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex);
                MessageBox.Show("Gagal mengubah alamat. Coba lagi.");
            }
        }

        private void BtnHapus_Click(object? sender, EventArgs e)
        {
            if (idTerpilih == 0)
            {
                MessageBox.Show("Klik dulu alamat yang mau dihapus di tabel.");
                return;
            }

            var yakin = MessageBox.Show("Yakin hapus alamat ini?", "Konfirmasi",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (yakin != DialogResult.Yes) return;

            try
            {
                using var conn = Koneksi.GetConnection();
                conn.Open();
                using var cmd = new MySqlCommand(
                    "DELETE FROM alamat_customer WHERE id_alamat=@id AND id_customer=@c", conn);
                cmd.Parameters.AddWithValue("@id", idTerpilih);
                cmd.Parameters.AddWithValue("@c", idCustomer);
                cmd.ExecuteNonQuery();

                MessageBox.Show("Alamat berhasil dihapus.");
                Bersihkan();
                MuatData();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex);
                MessageBox.Show("Gagal menghapus alamat. Coba lagi.");
            }
        }
    }
}