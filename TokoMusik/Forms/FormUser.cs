using System.Data;
using System.Text;
using MySql.Data.MySqlClient;
using TokoMusik.Data;
using TokoMusik.Models;

namespace TokoMusik.Forms
{
    public class FormUser : Form
    {
        private readonly TextBox txtCari = new();
        private readonly ComboBox cbCategory = new();
        private readonly DataGridView grid = new();
        private readonly PictureBox pic = new();
        private readonly Label lblNama = new();
        private readonly Label lblHarga = new();
        private readonly Label lblStok = new();
        private readonly TextBox txtInfo = new();
        private readonly NumericUpDown nudJumlah = new();
        private readonly Button btnTambah = new();
        private Button btnKeranjang = new();

        private int idProdukTerpilih = 0;
        private int stokTerpilih = 0;
        private string kodeTerpilih = "";
        private string namaTerpilih = "";
        private decimal hargaTerpilih = 0;

        public FormUser()
        {
            Text = "Toko Alat Musik";
            ClientSize = new Size(1150, 680);
            MinimumSize = new Size(1000, 620);
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Segoe UI", 10F);

            // ---------- panel menu (kiri) ----------
            var panelMenu = new Panel { Dock = DockStyle.Left, Width = 200, BackColor = Color.FromArgb(40, 44, 52) };

            var flowMenu = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true,
                Padding = new Padding(5, 5, 0, 5)
            };
            panelMenu.Controls.Add(flowMenu);

            var btnLogout = new Button
            {
                Text = "Logout",
                Dock = DockStyle.Bottom,
                Height = 50,
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.White,
                BackColor = Color.Firebrick
            };
            btnLogout.FlatAppearance.BorderSize = 0;
            btnLogout.Click += (s, e) =>
            {
                var yakin = MessageBox.Show("Yakin ingin logout?", "Logout",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (yakin == DialogResult.Yes) Close();
            };
            panelMenu.Controls.Add(btnLogout);

            panelMenu.Controls.Add(new Label
            {
                Text = "TOKO ALAT MUSIK\r\nHalo, " + Sesi.Username,
                Dock = DockStyle.Top,
                Height = 80,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(12, 0, 0, 0)
            });

            BuatMenu(flowMenu, "Katalog", () => MuatData());
            btnKeranjang = BuatMenu(flowMenu, "Keranjang", () => Buka(() => new FormKeranjang()));
            BuatMenu(flowMenu, "Pesanan Saya", () => Buka(() => new FormPesananSaya()));
            BuatMenu(flowMenu, "Alamat Saya", () => Buka(() => new FormAlamatSaya()));

            // ---------- area isi (kanan) ----------
            var panelIsi = new Panel { Dock = DockStyle.Fill };

            // tabel produk (Fill, ditambahkan paling awal)
            grid.Dock = DockStyle.Fill;
            grid.ReadOnly = true;
            grid.AllowUserToAddRows = false;
            grid.AllowUserToDeleteRows = false;
            grid.MultiSelect = false;
            grid.RowHeadersVisible = false;
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            grid.BackgroundColor = Color.White;
            grid.SelectionChanged += (s, e) => TampilDetail();
            panelIsi.Controls.Add(grid);

            // panel detail produk (kanan)
            var panelDetail = new Panel
            {
                Dock = DockStyle.Right,
                Width = 330,
                AutoScroll = true,
                BackColor = Color.White
            };

            pic.Bounds = new Rectangle(10, 10, 310, 230);
            pic.SizeMode = PictureBoxSizeMode.Zoom;
            pic.BorderStyle = BorderStyle.FixedSingle;
            pic.BackColor = Color.White;

            lblNama.Bounds = new Rectangle(10, 250, 310, 48);
            lblNama.Font = new Font("Segoe UI", 12F, FontStyle.Bold);

            lblHarga.Bounds = new Rectangle(10, 300, 310, 28);
            lblHarga.Font = new Font("Segoe UI", 13F, FontStyle.Bold);
            lblHarga.ForeColor = Color.SeaGreen;

            lblStok.Bounds = new Rectangle(10, 330, 310, 24);

            txtInfo.Bounds = new Rectangle(10, 358, 310, 150);
            txtInfo.Multiline = true;
            txtInfo.ReadOnly = true;
            txtInfo.ScrollBars = ScrollBars.Vertical;
            txtInfo.BackColor = Color.White;

            var lblJml = new Label { Text = "Jumlah:", Bounds = new Rectangle(10, 524, 60, 24) };
            nudJumlah.Bounds = new Rectangle(75, 520, 90, 28);
            nudJumlah.Minimum = 1;
            nudJumlah.Maximum = 1;

            btnTambah.Text = "Tambah ke Keranjang";
            btnTambah.Bounds = new Rectangle(10, 558, 310, 42);
            btnTambah.Font = new Font("Segoe UI", 10.5F, FontStyle.Bold);
            btnTambah.FlatStyle = FlatStyle.Flat;
            btnTambah.BackColor = Color.SeaGreen;
            btnTambah.ForeColor = Color.White;
            btnTambah.FlatAppearance.BorderSize = 0;
            btnTambah.Click += BtnTambah_Click;

            panelDetail.Controls.AddRange(new Control[]
            {
                pic, lblNama, lblHarga, lblStok, txtInfo, lblJml, nudJumlah, btnTambah
            });
            panelIsi.Controls.Add(panelDetail);

            // bar pencarian (atas)
            var panelAtas = new Panel { Dock = DockStyle.Top, Height = 54 };
            panelAtas.Controls.Add(new Label { Text = "Cari:", Bounds = new Rectangle(10, 16, 45, 24) });
            txtCari.Bounds = new Rectangle(55, 12, 300, 28);
            panelAtas.Controls.Add(txtCari);
            panelAtas.Controls.Add(new Label { Text = "Category:", Bounds = new Rectangle(375, 16, 75, 24) });
            cbCategory.Bounds = new Rectangle(450, 12, 220, 28);
            panelAtas.Controls.Add(cbCategory);
            panelIsi.Controls.Add(panelAtas);

            Controls.Add(panelIsi);
            Controls.Add(panelMenu);

            // ---------- isi filter category, lalu pasang event ----------
            try
            {
                var dt = new DataTable();
                dt.Columns.Add("Id", typeof(int));
                dt.Columns.Add("Nama", typeof(string));
                dt.Rows.Add(0, "Semua Category");

                using var conn = Koneksi.GetConnection();
                conn.Open();
                using var cmd = new MySqlCommand(
                    "SELECT id_category, nama_category FROM category ORDER BY nama_category", conn);
                using var r = cmd.ExecuteReader();
                while (r.Read())
                    dt.Rows.Add(r.GetInt32(0), r.GetString(1));

                cbCategory.DropDownStyle = ComboBoxStyle.DropDownList;
                cbCategory.DataSource = dt;
                cbCategory.DisplayMember = "Nama";
                cbCategory.ValueMember = "Id";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex);
                MessageBox.Show("Gagal memuat category. Coba lagi.");
            }

            txtCari.TextChanged += (s, e) => MuatData();
            cbCategory.SelectedIndexChanged += (s, e) => MuatData();

            KosongkanDetail();
            UpdateTombolKeranjang();

            Load += (s, e) =>
            {
                if (Sesi.Role != "user")
                {
                    MessageBox.Show("Halaman ini hanya untuk user.");
                    BeginInvoke(new Action(Close));
                    return;
                }
                MuatData();
            };

            FormClosed += (s, e) =>
            {
                pic.Image?.Dispose();
                Keranjang.Kosongkan();
            };
        }

        // ================= menu =================
        private Button BuatMenu(FlowLayoutPanel flow, string teks, Action aksi)
        {
            var btn = new Button
            {
                Text = teks,
                Width = 180,
                Height = 40,
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.White,
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(0, 0, 0, 2),
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(62, 68, 81);
            btn.Click += (s, e) => aksi();
            flow.Controls.Add(btn);
            return btn;
        }

        // membuka form lalu menyegarkan katalog (stok bisa berubah) dan jumlah isi keranjang
        private void Buka(Func<Form> buat)
        {
            try
            {
                using var f = buat();
                f.ShowDialog(this);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex);
                MessageBox.Show("Halaman tidak dapat dibuka. Coba lagi.");
            }
            UpdateTombolKeranjang();
            MuatData();
        }

        private void UpdateTombolKeranjang()
        {
            btnKeranjang.Text = "Keranjang (" + Keranjang.Items.Sum(i => i.Jumlah) + ")";
        }

        // ================= katalog =================
        private void MuatData()
        {
            try
            {
                int cat = cbCategory.SelectedValue is int v ? v : 0;

                using var conn = Koneksi.GetConnection();
                conn.Open();
                using var da = new MySqlDataAdapter(
                    "SELECT p.id_produk AS ID, p.kode_produk AS Kode, p.nama_produk AS Nama, " +
                    "c.nama_category AS Category, m.nama_merk AS Merk, p.harga_jual AS Harga, " +
                    "IFNULL(s.jumlah, 0) AS Stok " +
                    "FROM produk p " +
                    "JOIN category c ON c.id_category = p.id_category " +
                    "JOIN merk m ON m.id_merk = p.id_merk " +
                    "LEFT JOIN stock s ON s.id_produk = p.id_produk " +
                    "WHERE p.status = 'aktif' " +
                    "AND (p.nama_produk LIKE @cari OR p.kode_produk LIKE @cari) " +
                    "AND (@cat = 0 OR p.id_category = @cat) " +
                    "ORDER BY p.nama_produk", conn);
                da.SelectCommand.Parameters.AddWithValue("@cari", "%" + txtCari.Text.Trim() + "%");
                da.SelectCommand.Parameters.AddWithValue("@cat", cat);
                var dt = new DataTable();
                da.Fill(dt);
                grid.DataSource = dt;

                if (grid.Columns.Contains("ID")) grid.Columns["ID"]!.FillWeight = 20;
                if (grid.Columns.Contains("Stok")) grid.Columns["Stok"]!.FillWeight = 30;
                if (grid.Columns.Contains("Harga"))
                {
                    grid.Columns["Harga"]!.DefaultCellStyle.Format = "N0";
                    grid.Columns["Harga"]!.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                }

                if (dt.Rows.Count == 0) KosongkanDetail();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex);
                MessageBox.Show("Gagal memuat katalog. Coba lagi.");
            }
        }

        private void KosongkanDetail()
        {
            idProdukTerpilih = 0;
            stokTerpilih = 0;
            lblNama.Text = "Pilih produk di tabel";
            lblHarga.Text = "";
            lblStok.Text = "";
            txtInfo.Text = "";
            pic.Image?.Dispose();
            pic.Image = null;
            btnTambah.Enabled = false;
        }

        private void TampilDetail()
        {
            if (!grid.Columns.Contains("ID") || grid.CurrentRow == null ||
                grid.CurrentRow.Cells["ID"].Value is not int id)
            {
                KosongkanDetail();
                return;
            }

            var row = grid.CurrentRow;
            idProdukTerpilih = id;
            kodeTerpilih = row.Cells["Kode"].Value?.ToString() ?? "";
            namaTerpilih = row.Cells["Nama"].Value?.ToString() ?? "";
            hargaTerpilih = Convert.ToDecimal(row.Cells["Harga"].Value);
            stokTerpilih = Convert.ToInt32(row.Cells["Stok"].Value);

            lblNama.Text = namaTerpilih;
            lblHarga.Text = "Rp " + hargaTerpilih.ToString("N0");
            lblStok.Text = stokTerpilih > 0 ? "Stok tersedia: " + stokTerpilih.ToString("N0") : "Stok habis";

            nudJumlah.Value = 1;
            nudJumlah.Maximum = Math.Max(1, stokTerpilih);
            btnTambah.Enabled = stokTerpilih > 0;

            try
            {
                using var conn = Koneksi.GetConnection();
                conn.Open();
                using var cmd = new MySqlCommand(
                    "SELECT p.deskripsi, p.foto, d.warna, d.bahan, d.ukuran, d.berat_gram, d.spesifikasi " +
                    "FROM produk p LEFT JOIN detail_produk d ON d.id_produk = p.id_produk " +
                    "WHERE p.id_produk = @id", conn);
                cmd.Parameters.AddWithValue("@id", id);
                using var r = cmd.ExecuteReader();
                if (!r.Read()) return;

                var sb = new StringBuilder();
                Tambah(sb, "", r["deskripsi"]);
                Tambah(sb, "Warna: ", r["warna"]);
                Tambah(sb, "Bahan: ", r["bahan"]);
                Tambah(sb, "Ukuran: ", r["ukuran"]);
                if (r["berat_gram"] != DBNull.Value)
                    sb.AppendLine("Berat: " + Convert.ToInt32(r["berat_gram"]).ToString("N0") + " gram");
                Tambah(sb, "Spesifikasi: ", r["spesifikasi"]);
                txtInfo.Text = sb.ToString();

                var img = FotoHelper.Muat(r["foto"] == DBNull.Value ? null : r["foto"].ToString());
                pic.Image?.Dispose();
                pic.Image = img;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex);
            }
        }

        private static void Tambah(StringBuilder sb, string awalan, object nilai)
        {
            if (nilai == DBNull.Value) return;
            var s = nilai.ToString();
            if (string.IsNullOrWhiteSpace(s)) return;
            sb.AppendLine(awalan + s);
        }

        // ================= tambah ke keranjang =================
        private void BtnTambah_Click(object? sender, EventArgs e)
        {
            if (idProdukTerpilih == 0) return;

            int jumlah = (int)nudJumlah.Value;
            var ada = Keranjang.Items.FirstOrDefault(i => i.IdProduk == idProdukTerpilih);
            int total = (ada?.Jumlah ?? 0) + jumlah;

            if (total > stokTerpilih)
            {
                MessageBox.Show("Jumlah di keranjang melebihi stok (stok " + stokTerpilih.ToString("N0") +
                                ", sudah di keranjang " + (ada?.Jumlah ?? 0).ToString("N0") + ").");
                return;
            }

            Keranjang.Tambah(new ItemKeranjang
            {
                IdProduk = idProdukTerpilih,
                Kode = kodeTerpilih,
                Nama = namaTerpilih,
                Harga = hargaTerpilih,
                Jumlah = jumlah
            });

            UpdateTombolKeranjang();
            MessageBox.Show(jumlah + " x " + namaTerpilih + " ditambahkan ke keranjang.");
        }
    }
}