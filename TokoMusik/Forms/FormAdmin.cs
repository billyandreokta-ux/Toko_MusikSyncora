using TokoMusik.Models;

namespace TokoMusik.Forms
{
    public class FormAdmin : Form
    {
        private readonly FlowLayoutPanel flowMenu = new();

        public FormAdmin()
        {
            Text = "Dashboard Admin - Toko Alat Musik";
            ClientSize = new Size(1000, 600);
            MinimumSize = new Size(800, 500);
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Segoe UI", 10F);

            // ---------- area isi (kanan) ----------
            var panelIsi = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(240, 240, 240) };

            var lblWelcome = new Label
            {
                Text = $"Selamat datang, {Sesi.Username} (Admin)",
                Font = new Font("Segoe UI", 14F, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(30, 30)
            };
            var lblInfo = new Label
            {
                Text = "Pilih menu di sebelah kiri untuk mengelola data.",
                AutoSize = true,
                Location = new Point(32, 75)
            };
            panelIsi.Controls.Add(lblWelcome);
            panelIsi.Controls.Add(lblInfo);

            // ---------- panel menu (kiri) ----------
            var panelMenu = new Panel
            {
                Dock = DockStyle.Left,
                Width = 220,
                BackColor = Color.FromArgb(40, 44, 52)
            };

            flowMenu.Dock = DockStyle.Fill;
            flowMenu.FlowDirection = FlowDirection.TopDown;
            flowMenu.WrapContents = false;
            flowMenu.AutoScroll = true;
            flowMenu.Padding = new Padding(5, 5, 0, 5);
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

            var lblMenu = new Label
            {
                Text = "MENU ADMIN",
                Dock = DockStyle.Top,
                Height = 60,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(12, 0, 0, 0)
            };
            panelMenu.Controls.Add(lblMenu);

            Controls.Add(panelIsi);
            Controls.Add(panelMenu);

            // ---------- daftar menu ----------
            TambahJudul("DATA MASTER");

            TambahMenu("Category", () => new FormCategory());
            TambahMenu("Merk", () => new FormMerk());

            TambahMenu("Supplier", () => new FormMaster(
                "Data Supplier", "supplier", "id_supplier", "nama_supplier",
                new List<FieldDef>
                {
                    new() { Kolom = "nama_supplier", Label = "Nama Supplier", Wajib = true },
                    new() { Kolom = "telepon", Label = "Telepon" },
                    new() { Kolom = "email", Label = "Email" },
                    new() { Kolom = "alamat", Label = "Alamat" }
                }));

            TambahMenu("Metode Pembayaran", () => new FormMaster(
                "Data Metode Pembayaran", "metode_pembayaran", "id_metode", "nama_metode",
                new List<FieldDef>
                {
                    new() { Kolom = "nama_metode", Label = "Nama Metode", Wajib = true },
                    new() { Kolom = "keterangan", Label = "Keterangan" }
                }));

            TambahMenu("Karyawan", () => new FormMaster(
                "Data Karyawan", "karyawan", "id_karyawan", "nama_karyawan",
                new List<FieldDef>
                {
                    new() { Kolom = "nama_karyawan", Label = "Nama Karyawan", Wajib = true },
                    new() { Kolom = "jabatan", Label = "Jabatan" },
                    new() { Kolom = "telepon", Label = "Telepon" },
                    new() { Kolom = "alamat", Label = "Alamat" },
                    new() { Kolom = "gaji", Label = "Gaji", Tipe = TipeField.Angka },
                    new() { Kolom = "tgl_masuk", Label = "Tanggal Masuk", Tipe = TipeField.Tanggal }
                }));

            TambahMenu("Promo", () => new FormMaster(
                "Data Promo", "promo", "id_promo", "nama_promo",
                new List<FieldDef>
                {
                    new() { Kolom = "kode_promo", Label = "Kode Promo", Wajib = true },
                    new() { Kolom = "nama_promo", Label = "Nama Promo", Wajib = true },
                    new() { Kolom = "tipe", Label = "Tipe Diskon", Tipe = TipeField.Pilihan,
                            Pilihan = new[] { "persen", "nominal" }, Wajib = true },
                    new() { Kolom = "nilai", Label = "Nilai (persen atau rupiah)", Tipe = TipeField.Angka,
                            Wajib = true, Positif = true },
                    new() { Kolom = "min_belanja", Label = "Minimal Belanja", Tipe = TipeField.Angka },
                    new() { Kolom = "tgl_mulai", Label = "Tanggal Mulai", Tipe = TipeField.Tanggal, Wajib = true },
                    new() { Kolom = "tgl_selesai", Label = "Tanggal Selesai", Tipe = TipeField.Tanggal, Wajib = true },
                    new() { Kolom = "status", Label = "Status", Tipe = TipeField.Pilihan,
                            Pilihan = new[] { "aktif", "nonaktif" }, Wajib = true }
                },
                d =>
                {
                    if ((string)d["tipe"] == "persen" && (decimal)d["nilai"] > 100)
                        return "Diskon persen tidak boleh lebih dari 100.";
                    if ((DateTime)d["tgl_selesai"] < (DateTime)d["tgl_mulai"])
                        return "Tanggal selesai tidak boleh sebelum tanggal mulai.";
                    return null;
                }));

            TambahMenu("Pengeluaran", () => new FormMaster(
                "Data Pengeluaran", "pengeluaran", "id_pengeluaran", "kategori",
                new List<FieldDef>
                {
                    new() { Kolom = "id_karyawan", Label = "Karyawan", Tipe = TipeField.Lookup,
                            LookupSql = "SELECT id_karyawan, nama_karyawan FROM karyawan ORDER BY nama_karyawan",
                            TampilSql = "(SELECT k.nama_karyawan FROM karyawan k WHERE k.id_karyawan = t.id_karyawan)" },
                    new() { Kolom = "tanggal", Label = "Tanggal", Tipe = TipeField.Tanggal, Wajib = true },
                    new() { Kolom = "kategori", Label = "Kategori" },
                    new() { Kolom = "jumlah", Label = "Jumlah (Rp)", Tipe = TipeField.Angka, Wajib = true, Positif = true },
                    new() { Kolom = "keterangan", Label = "Keterangan" }
                }));

            TambahJudul("DATA PRODUK");

            TambahMenu("Produk", () => new FormProduk());
            TambahMenu("Pembelian Stok", () => new FormPurchaseStock());

            TambahJudul("TRANSAKSI");

            TambahMenu("Pesanan", () => new FormPesananAdmin());

            // ---------- pengaman: hanya admin ----------
            Load += (s, e) =>
            {
                if (Sesi.Role != "admin")
                {
                    MessageBox.Show("Halaman ini hanya untuk admin.");
                    Close();
                }
            };
        }

        private void TambahJudul(string teks)
        {
            flowMenu.Controls.Add(new Label
            {
                Text = teks,
                Width = 195,
                Height = 28,
                ForeColor = Color.Gray,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                TextAlign = ContentAlignment.BottomLeft,
                Margin = new Padding(0, 8, 0, 0)
            });
        }

        private void TambahMenu(string teks, Func<Form> buat)
        {
            var btn = new Button
            {
                Text = teks,
                Width = 195,
                Height = 40,
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.White,
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(0, 0, 0, 2),
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(62, 68, 81);
            btn.Click += (s, e) => BukaForm(buat);
            flowMenu.Controls.Add(btn);
        }

        private void BukaForm(Func<Form> buat)
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
        }
    }
}