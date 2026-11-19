using System.Data;
using MySql.Data.MySqlClient;
using TokoMusik.Data;
using TokoMusik.Models;

namespace TokoMusik.Forms
{
    public class FormKeranjang : Form
    {
        private readonly DataGridView grid = new();
        private readonly NumericUpDown nudJumlah = new();
        private readonly ComboBox cbAlamat = new();
        private readonly ComboBox cbMetode = new();
        private readonly TextBox txtPromo = new();
        private readonly Label lblSubtotal = new();
        private readonly Label lblDiskon = new();
        private readonly Label lblOngkir = new();
        private readonly Label lblTotal = new();

        private int idCustomer = 0;
        private string kodeAktif = "";

        public FormKeranjang()
        {
            Text = "Keranjang Belanja";
            ClientSize = new Size(900, 590);
            MinimumSize = Size;
            StartPosition = FormStartPosition.CenterParent;
            Font = new Font("Segoe UI", 10F);

            try { idCustomer = PesananService.GetIdCustomer() ?? 0; }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }

            // ---------- isi keranjang ----------
            Controls.Add(new Label
            {
                Text = "Isi Keranjang",
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                Bounds = new Rectangle(20, 8, 300, 24)
            });

            grid.Bounds = new Rectangle(20, 36, 860, 216);
            grid.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            grid.ReadOnly = true;
            grid.AllowUserToAddRows = false;
            grid.AllowUserToDeleteRows = false;
            grid.MultiSelect = false;
            grid.RowHeadersVisible = false;
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            grid.BackgroundColor = Color.White;
            grid.SelectionChanged += (s, e) => SinkronJumlah();
            Controls.Add(grid);

            Controls.Add(new Label { Text = "Jumlah:", Bounds = new Rectangle(20, 268, 60, 24) });
            nudJumlah.Bounds = new Rectangle(85, 264, 90, 28);
            nudJumlah.Minimum = 1;
            nudJumlah.Maximum = 9999;
            Controls.Add(nudJumlah);

            Controls.Add(BuatTombol("Ubah Jumlah", 190, 262, 130, BtnUbahJumlah_Click));
            Controls.Add(BuatTombol("Hapus Item", 330, 262, 120, BtnHapusItem_Click));
            Controls.Add(BuatTombol("Kosongkan", 460, 262, 120, BtnKosongkan_Click));

            // ---------- pengiriman, pembayaran, promo ----------
            Controls.Add(new Label { Text = "Alamat Pengiriman *", Bounds = new Rectangle(20, 316, 330, 22) });
            cbAlamat.Bounds = new Rectangle(20, 340, 330, 28);
            Controls.Add(cbAlamat);
            Controls.Add(BuatTombol("Kelola Alamat", 360, 338, 130, BtnKelolaAlamat_Click));

            Controls.Add(new Label { Text = "Metode Pembayaran *", Bounds = new Rectangle(20, 384, 330, 22) });
            cbMetode.Bounds = new Rectangle(20, 408, 330, 28);
            Controls.Add(cbMetode);

            Controls.Add(new Label { Text = "Kode Promo (opsional)", Bounds = new Rectangle(20, 452, 330, 22) });
            txtPromo.Bounds = new Rectangle(20, 476, 230, 28);
            txtPromo.MaxLength = 30;
            Controls.Add(txtPromo);
            Controls.Add(BuatTombol("Terapkan", 260, 474, 90, BtnTerapkan_Click));

            // ---------- ringkasan ----------
            BuatLabelRingkas(lblSubtotal, 330, false);
            BuatLabelRingkas(lblDiskon, 360, false);
            BuatLabelRingkas(lblOngkir, 390, false);
            BuatLabelRingkas(lblTotal, 430, true);

            var btnBuat = new Button
            {
                Text = "Buat Pesanan",
                Bounds = new Rectangle(540, 490, 340, 48),
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.SeaGreen,
                ForeColor = Color.White
            };
            btnBuat.FlatAppearance.BorderSize = 0;
            btnBuat.Click += BtnBuat_Click;
            Controls.Add(btnBuat);

            // ---------- data awal ----------
            IsiGrid();
            MuatAlamat();
            MuatMetode();
            Ringkas();

            Load += (s, e) =>
            {
                if (idCustomer == 0)
                {
                    MessageBox.Show("Data customer untuk akun ini tidak ditemukan. Hubungi admin.");
                    BeginInvoke(new Action(Close));
                }
            };
        }

        // ================= tata letak =================
        private static Button BuatTombol(string teks, int x, int y, int lebar, EventHandler aksi)
        {
            var b = new Button { Text = teks, Bounds = new Rectangle(x, y, lebar, 32) };
            b.Click += aksi;
            return b;
        }

        private void BuatLabelRingkas(Label l, int y, bool tebal)
        {
            l.Bounds = new Rectangle(540, y, 340, tebal ? 32 : 26);
            l.TextAlign = ContentAlignment.MiddleRight;
            l.Font = new Font("Segoe UI", tebal ? 14F : 10.5F, tebal ? FontStyle.Bold : FontStyle.Regular);
            Controls.Add(l);
        }

        private static void IsiCombo(ComboBox cb, string sql, int? idCustomer = null)
        {
            var dt = new DataTable();
            dt.Columns.Add("Id", typeof(int));
            dt.Columns.Add("Nama", typeof(string));

            using var conn = Koneksi.GetConnection();
            conn.Open();
            using var cmd = new MySqlCommand(sql, conn);
            if (idCustomer.HasValue) cmd.Parameters.AddWithValue("@c", idCustomer.Value);
            using var r = cmd.ExecuteReader();
            while (r.Read())
                dt.Rows.Add(r.GetInt32(0), r.GetString(1));

            cb.DropDownStyle = ComboBoxStyle.DropDownList;
            cb.DataSource = dt;
            cb.DisplayMember = "Nama";
            cb.ValueMember = "Id";
        }

        private void MuatAlamat()
        {
            if (idCustomer == 0) return;
            try
            {
                IsiCombo(cbAlamat,
                    "SELECT id_alamat, CONCAT(IFNULL(label_alamat, 'Alamat'), ' - ', penerima, ', ', IFNULL(kota, '')) " +
                    "FROM alamat_customer WHERE id_customer=@c ORDER BY is_utama DESC, id_alamat DESC",
                    idCustomer);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex);
                MessageBox.Show("Gagal memuat alamat. Coba lagi.");
            }
        }

        private void MuatMetode()
        {
            try
            {
                IsiCombo(cbMetode, "SELECT id_metode, nama_metode FROM metode_pembayaran ORDER BY nama_metode");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex);
                MessageBox.Show("Gagal memuat metode pembayaran. Coba lagi.");
            }
        }

        // ================= tabel keranjang =================
        private void IsiGrid()
        {
            var dt = new DataTable();
            dt.Columns.Add("ID", typeof(int));
            dt.Columns.Add("Kode", typeof(string));
            dt.Columns.Add("Nama", typeof(string));
            dt.Columns.Add("Harga", typeof(decimal));
            dt.Columns.Add("Jumlah", typeof(int));
            dt.Columns.Add("Subtotal", typeof(decimal));

            foreach (var i in Keranjang.Items)
                dt.Rows.Add(i.IdProduk, i.Kode, i.Nama, i.Harga, i.Jumlah, i.Harga * i.Jumlah);

            grid.DataSource = dt;

            if (grid.Columns.Contains("ID")) grid.Columns["ID"]!.FillWeight = 20;
            if (grid.Columns.Contains("Jumlah")) grid.Columns["Jumlah"]!.FillWeight = 35;
            foreach (var nama in new[] { "Harga", "Subtotal" })
            {
                if (!grid.Columns.Contains(nama)) continue;
                grid.Columns[nama]!.DefaultCellStyle.Format = "N0";
                grid.Columns[nama]!.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            }
        }

        private int? IdTerpilih()
        {
            if (grid.CurrentRow == null || !grid.Columns.Contains("ID")) return null;
            var v = grid.CurrentRow.Cells["ID"].Value;
            if (v == null || v == DBNull.Value) return null;
            return Convert.ToInt32(v);
        }

        private void SinkronJumlah()
        {
            var id = IdTerpilih();
            if (id == null) return;
            var item = Keranjang.Items.FirstOrDefault(i => i.IdProduk == id.Value);
            if (item != null && item.Jumlah >= nudJumlah.Minimum && item.Jumlah <= nudJumlah.Maximum)
                nudJumlah.Value = item.Jumlah;
        }

        // ================= ringkasan harga =================
        private void Ringkas()
        {
            decimal subtotal = Keranjang.Subtotal();
            decimal diskon = 0;

            if (kodeAktif != "" && Keranjang.Items.Count > 0)
            {
                try
                {
                    diskon = PesananService.PreviewDiskon(kodeAktif, subtotal);
                }
                catch (PesananService.PesananException ex)
                {
                    MessageBox.Show(ex.Message);
                    kodeAktif = "";
                    txtPromo.Clear();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(ex);
                    kodeAktif = "";
                    txtPromo.Clear();
                }
            }

            decimal ongkir = Keranjang.Items.Count > 0 ? PesananService.Ongkir : 0;

            lblSubtotal.Text = "Subtotal: Rp " + subtotal.ToString("N0");
            lblDiskon.Text = "Diskon promo: - Rp " + diskon.ToString("N0");
            lblOngkir.Text = "Ongkos kirim: Rp " + ongkir.ToString("N0");
            lblTotal.Text = "TOTAL: Rp " + (subtotal - diskon + ongkir).ToString("N0");
        }

        // ================= aksi =================
        private void BtnUbahJumlah_Click(object? sender, EventArgs e)
        {
            var id = IdTerpilih();
            if (id == null)
            {
                MessageBox.Show("Klik dulu item yang mau diubah jumlahnya.");
                return;
            }

            int jumlah = (int)nudJumlah.Value;
            int stok;
            try
            {
                stok = PesananService.AmbilStok(id.Value);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex);
                MessageBox.Show("Gagal memeriksa stok. Coba lagi.");
                return;
            }

            if (jumlah > stok)
            {
                MessageBox.Show("Stok hanya tersedia " + stok.ToString("N0") + ".");
                return;
            }

            var item = Keranjang.Items.FirstOrDefault(i => i.IdProduk == id.Value);
            if (item != null) item.Jumlah = jumlah;

            IsiGrid();
            Ringkas();
        }

        private void BtnHapusItem_Click(object? sender, EventArgs e)
        {
            var id = IdTerpilih();
            if (id == null)
            {
                MessageBox.Show("Klik dulu item yang mau dihapus.");
                return;
            }

            Keranjang.Items.RemoveAll(i => i.IdProduk == id.Value);
            IsiGrid();
            Ringkas();
        }

        private void BtnKosongkan_Click(object? sender, EventArgs e)
        {
            if (Keranjang.Items.Count == 0) return;

            var yakin = MessageBox.Show("Kosongkan seluruh isi keranjang?", "Konfirmasi",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (yakin != DialogResult.Yes) return;

            Keranjang.Kosongkan();
            IsiGrid();
            Ringkas();
        }

        private void BtnKelolaAlamat_Click(object? sender, EventArgs e)
        {
            using (var f = new FormAlamatSaya())
                f.ShowDialog(this);
            MuatAlamat();
        }

        private void BtnTerapkan_Click(object? sender, EventArgs e)
        {
            kodeAktif = txtPromo.Text.Trim();
            Ringkas();
            if (kodeAktif != "")
                MessageBox.Show("Promo berhasil diterapkan.");
        }

        private void BtnBuat_Click(object? sender, EventArgs e)
        {
            if (Keranjang.Items.Count == 0)
            {
                MessageBox.Show("Keranjang masih kosong.");
                return;
            }
            if (cbAlamat.SelectedValue is not int idAlamat)
            {
                MessageBox.Show("Pilih alamat pengiriman dulu. Klik \"Kelola Alamat\" untuk menambah alamat.");
                return;
            }
            if (cbMetode.SelectedValue is not int idMetode)
            {
                MessageBox.Show("Pilih metode pembayaran dulu.");
                return;
            }

            var yakin = MessageBox.Show(
                "Buat pesanan dengan " + lblTotal.Text.ToLower() + "?", "Konfirmasi",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (yakin != DialogResult.Yes) return;

            try
            {
                int idPesanan = PesananService.Checkout(idCustomer, idAlamat, idMetode, kodeAktif);
                MessageBox.Show(
                    "Pesanan #" + idPesanan + " berhasil dibuat.\n" +
                    "Silakan lakukan pembayaran sesuai metode yang dipilih. Admin akan memverifikasinya.",
                    "Pesanan Berhasil");
                Close();
            }
            catch (PesananService.PesananException ex)
            {
                MessageBox.Show(ex.Message);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex);
                MessageBox.Show("Gagal membuat pesanan. Coba lagi.");
            }
        }
    }
}