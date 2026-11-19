using System.Data;
using MySql.Data.MySqlClient;
using TokoMusik.Data;

namespace TokoMusik.Forms
{
    public class FormCategory : Form
    {
        private readonly TextBox txtNama = new();
        private readonly TextBox txtDeskripsi = new();
        private readonly TextBox txtCari = new();
        private readonly DataGridView grid = new();
        private int idTerpilih = 0;

        public FormCategory()
        {
            Text = "Data Category";
            ClientSize = new Size(780, 520);
            StartPosition = FormStartPosition.CenterParent;
            Font = new Font("Segoe UI", 10F);

            var lblNama = new Label { Text = "Nama Category", Bounds = new Rectangle(20, 15, 300, 22) };
            txtNama.Bounds = new Rectangle(20, 39, 300, 28);
            txtNama.MaxLength = 100;   // sama dengan kolom di database
            var lblDesk = new Label { Text = "Deskripsi", Bounds = new Rectangle(340, 15, 300, 22) };
            txtDeskripsi.Bounds = new Rectangle(340, 39, 420, 28);

            var btnTambah = BuatTombol("Tambah", 20, BtnTambah_Click);
            var btnUbah = BuatTombol("Ubah", 140, BtnUbah_Click);
            var btnHapus = BuatTombol("Hapus", 260, BtnHapus_Click);
            var btnBersih = BuatTombol("Bersihkan", 380, (s, e) => Bersihkan());

            var lblCari = new Label { Text = "Cari:", Bounds = new Rectangle(20, 133, 50, 22) };
            txtCari.Bounds = new Rectangle(70, 130, 280, 28);
            txtCari.TextChanged += (s, e) => MuatData();

            grid.Bounds = new Rectangle(20, 170, 740, 330);
            grid.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            grid.ReadOnly = true;
            grid.AllowUserToAddRows = false;
            grid.AllowUserToDeleteRows = false;
            grid.MultiSelect = false;
            grid.RowHeadersVisible = false;
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            grid.BackgroundColor = Color.White;   // area kosong di bawah baris jadi putih
            grid.CellClick += Grid_CellClick;

            Controls.AddRange(new Control[]
            {
                lblNama, txtNama, lblDesk, txtDeskripsi,
                btnTambah, btnUbah, btnHapus, btnBersih,
                lblCari, txtCari, grid
            });

            Load += (s, e) => MuatData();
        }

        private Button BuatTombol(string teks, int x, EventHandler aksi)
        {
            var b = new Button { Text = teks, Bounds = new Rectangle(x, 80, 110, 34) };
            b.Click += aksi;
            return b;
        }

        private void MuatData()
        {
            try
            {
                using var conn = Koneksi.GetConnection();
                conn.Open();
                using var da = new MySqlDataAdapter(
                    "SELECT id_category AS ID, nama_category AS Nama, deskripsi AS Deskripsi " +
                    "FROM category WHERE nama_category LIKE @cari ORDER BY nama_category", conn);
                da.SelectCommand.Parameters.AddWithValue("@cari", "%" + txtCari.Text.Trim() + "%");
                var dt = new DataTable();
                da.Fill(dt);
                grid.DataSource = dt;
                if (grid.Columns.Contains("ID")) grid.Columns["ID"]!.FillWeight = 20;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex);
                MessageBox.Show("Gagal memuat data. Coba lagi.");
            }
        }

        private void Grid_CellClick(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;
            var row = grid.Rows[e.RowIndex];
            idTerpilih = Convert.ToInt32(row.Cells["ID"].Value);
            txtNama.Text = row.Cells["Nama"].Value?.ToString() ?? "";
            txtDeskripsi.Text = row.Cells["Deskripsi"].Value?.ToString() ?? "";
        }

        private void Bersihkan()
        {
            idTerpilih = 0;
            txtNama.Clear();
            txtDeskripsi.Clear();
            grid.ClearSelection();
            txtNama.Focus();
        }

        private void BtnTambah_Click(object? sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtNama.Text))
            {
                MessageBox.Show("Nama category wajib diisi.");
                return;
            }

            try
            {
                using var conn = Koneksi.GetConnection();
                conn.Open();
                using var cmd = new MySqlCommand(
                    "INSERT INTO category (nama_category, deskripsi) VALUES (@n, @d)", conn);
                cmd.Parameters.AddWithValue("@n", txtNama.Text.Trim());
                cmd.Parameters.AddWithValue("@d", txtDeskripsi.Text.Trim());
                cmd.ExecuteNonQuery();
                MessageBox.Show("Data berhasil ditambahkan.");
                Bersihkan();
                MuatData();
            }
            catch (MySqlException ex) when (ex.Number == 1062)
            {
                MessageBox.Show("Nama category sudah ada.");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex);
                MessageBox.Show("Gagal menambah data. Coba lagi.");
            }
        }

        private void BtnUbah_Click(object? sender, EventArgs e)
        {
            if (idTerpilih == 0)
            {
                MessageBox.Show("Klik dulu data yang mau diubah di tabel.");
                return;
            }
            if (string.IsNullOrWhiteSpace(txtNama.Text))
            {
                MessageBox.Show("Nama category wajib diisi.");
                return;
            }

            try
            {
                using var conn = Koneksi.GetConnection();
                conn.Open();
                using var cmd = new MySqlCommand(
                    "UPDATE category SET nama_category=@n, deskripsi=@d WHERE id_category=@id", conn);
                cmd.Parameters.AddWithValue("@n", txtNama.Text.Trim());
                cmd.Parameters.AddWithValue("@d", txtDeskripsi.Text.Trim());
                cmd.Parameters.AddWithValue("@id", idTerpilih);
                cmd.ExecuteNonQuery();
                MessageBox.Show("Data berhasil diubah.");
                Bersihkan();
                MuatData();
            }
            catch (MySqlException ex) when (ex.Number == 1062)
            {
                MessageBox.Show("Nama category sudah dipakai data lain.");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex);
                MessageBox.Show("Gagal mengubah data. Coba lagi.");
            }
        }

        private void BtnHapus_Click(object? sender, EventArgs e)
        {
            if (idTerpilih == 0)
            {
                MessageBox.Show("Klik dulu data yang mau dihapus di tabel.");
                return;
            }

            var yakin = MessageBox.Show("Yakin hapus category ini?", "Konfirmasi",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (yakin != DialogResult.Yes) return;

            try
            {
                using var conn = Koneksi.GetConnection();
                conn.Open();
                using var cmd = new MySqlCommand("DELETE FROM category WHERE id_category=@id", conn);
                cmd.Parameters.AddWithValue("@id", idTerpilih);
                cmd.ExecuteNonQuery();
                MessageBox.Show("Data berhasil dihapus.");
                Bersihkan();
                MuatData();
            }
            catch (MySqlException ex) when (ex.Number == 1451)
            {
                MessageBox.Show("Category ini tidak bisa dihapus karena masih dipakai oleh produk.");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex);
                MessageBox.Show("Gagal menghapus data. Coba lagi.");
            }
        }
    }
}