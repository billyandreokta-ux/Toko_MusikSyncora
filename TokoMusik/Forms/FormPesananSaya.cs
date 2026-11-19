using System.Data;
using MySql.Data.MySqlClient;
using TokoMusik.Data;

namespace TokoMusik.Forms
{
    public class FormPesananSaya : Form
    {
        private readonly DataGridView gridPesanan = new();
        private readonly DataGridView gridDetail = new();

        private int idCustomer = 0;
        private int idTerpilih = 0;
        private string statusTerpilih = "";

        public FormPesananSaya()
        {
            Text = "Pesanan Saya";
            ClientSize = new Size(900, 620);
            MinimumSize = new Size(800, 560);
            StartPosition = FormStartPosition.CenterParent;
            Font = new Font("Segoe UI", 10F);

            try { idCustomer = PesananService.GetIdCustomer() ?? 0; }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }

            Controls.Add(BuatTombol("Batalkan Pesanan", 20, 10, 170, BtnBatal_Click));
            Controls.Add(BuatTombol("Pesanan Diterima", 200, 10, 170, BtnDiterima_Click));
            Controls.Add(BuatTombol("Refresh", 380, 10, 110, (s, e) => MuatPesanan()));

            Controls.Add(new Label
            {
                Text = "Klik pesanan untuk melihat rinciannya. Pembatalan hanya untuk pesanan yang belum dibayar.",
                ForeColor = Color.Gray,
                Bounds = new Rectangle(500, 16, 380, 22)
            });

            SiapkanGrid(gridPesanan, new Rectangle(20, 54, 860, 250));
            gridPesanan.CellClick += GridPesanan_CellClick;
            gridPesanan.DataBindingComplete += (s, e) =>
            {
                gridPesanan.ClearSelection();
                idTerpilih = 0;
                statusTerpilih = "";
                gridDetail.DataSource = null;
            };

            Controls.Add(new Label
            {
                Text = "Rincian Pesanan",
                Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
                Bounds = new Rectangle(20, 316, 300, 24)
            });
            SiapkanGrid(gridDetail, new Rectangle(20, 344, 860, 256));
            gridDetail.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;

            Load += (s, e) =>
            {
                if (idCustomer == 0)
                {
                    MessageBox.Show("Data customer untuk akun ini tidak ditemukan. Hubungi admin.");
                    BeginInvoke(new Action(Close));
                    return;
                }
                MuatPesanan();
            };
        }

        private void SiapkanGrid(DataGridView g, Rectangle posisi)
        {
            g.Bounds = posisi;
            g.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            g.ReadOnly = true;
            g.AllowUserToAddRows = false;
            g.AllowUserToDeleteRows = false;
            g.MultiSelect = false;
            g.RowHeadersVisible = false;
            g.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            g.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            g.BackgroundColor = Color.White;
            Controls.Add(g);
        }

        private static Button BuatTombol(string teks, int x, int y, int lebar, EventHandler aksi)
        {
            var b = new Button { Text = teks, Bounds = new Rectangle(x, y, lebar, 34) };
            b.Click += aksi;
            return b;
        }

        private static void FormatUang(DataGridView g, params string[] kolom)
        {
            foreach (var nama in kolom)
            {
                if (!g.Columns.Contains(nama)) continue;
                g.Columns[nama]!.DefaultCellStyle.Format = "N0";
                g.Columns[nama]!.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            }
        }

        // ================= daftar pesanan =================
        private void MuatPesanan()
        {
            if (idCustomer == 0) return;
            try
            {
                using var conn = Koneksi.GetConnection();
                conn.Open();
                using var da = new MySqlDataAdapter(
                    "SELECT p.id_pemesanan AS ID, p.tgl_pesan AS Tanggal, p.subtotal AS Subtotal, " +
                    "p.diskon AS Diskon, p.ongkir AS Ongkir, p.total_bayar AS Total, p.status AS Status, " +
                    "IFNULL((SELECT py.status FROM payments py WHERE py.id_pemesanan = p.id_pemesanan " +
                    "ORDER BY py.id_payment DESC LIMIT 1), '-') AS Pembayaran " +
                    "FROM pemesanan p WHERE p.id_customer=@c ORDER BY p.id_pemesanan DESC", conn);
                da.SelectCommand.Parameters.AddWithValue("@c", idCustomer);
                var dt = new DataTable();
                da.Fill(dt);
                gridPesanan.DataSource = dt;

                if (gridPesanan.Columns.Contains("ID")) gridPesanan.Columns["ID"]!.FillWeight = 20;
                if (gridPesanan.Columns.Contains("Tanggal"))
                    gridPesanan.Columns["Tanggal"]!.DefaultCellStyle.Format = "dd/MM/yyyy HH:mm";
                FormatUang(gridPesanan, "Subtotal", "Diskon", "Ongkir", "Total");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex);
                MessageBox.Show("Gagal memuat pesanan. Coba lagi.");
            }
        }

        private void GridPesanan_CellClick(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;
            var row = gridPesanan.Rows[e.RowIndex];
            idTerpilih = Convert.ToInt32(row.Cells["ID"].Value);
            statusTerpilih = row.Cells["Status"].Value?.ToString() ?? "";
            MuatDetail();
        }

        private void MuatDetail()
        {
            try
            {
                using var conn = Koneksi.GetConnection();
                conn.Open();
                using var da = new MySqlDataAdapter(
                    "SELECT pr.kode_produk AS Kode, pr.nama_produk AS Produk, d.jumlah AS Jumlah, " +
                    "d.harga_satuan AS Harga, d.subtotal AS Subtotal " +
                    "FROM detail_pesanan d JOIN produk pr ON pr.id_produk = d.id_produk " +
                    "WHERE d.id_pemesanan=@id", conn);
                da.SelectCommand.Parameters.AddWithValue("@id", idTerpilih);
                var dt = new DataTable();
                da.Fill(dt);
                gridDetail.DataSource = dt;

                if (gridDetail.Columns.Contains("Jumlah")) gridDetail.Columns["Jumlah"]!.FillWeight = 35;
                FormatUang(gridDetail, "Harga", "Subtotal");
                gridDetail.ClearSelection();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex);
                MessageBox.Show("Gagal memuat rincian pesanan. Coba lagi.");
            }
        }

        // ================= aksi =================
        private void BtnBatal_Click(object? sender, EventArgs e)
        {
            if (idTerpilih == 0)
            {
                MessageBox.Show("Klik dulu pesanan yang mau dibatalkan.");
                return;
            }
            if (statusTerpilih != "menunggu_bayar")
            {
                MessageBox.Show("Hanya pesanan yang belum dibayar yang bisa dibatalkan.");
                return;
            }

            var yakin = MessageBox.Show("Yakin batalkan pesanan #" + idTerpilih + "?", "Konfirmasi",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (yakin != DialogResult.Yes) return;

            try
            {
                PesananService.Batalkan(idTerpilih, idCustomer);
                MessageBox.Show("Pesanan dibatalkan. Stok sudah dikembalikan.");
                MuatPesanan();
            }
            catch (PesananService.PesananException ex)
            {
                MessageBox.Show(ex.Message);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex);
                MessageBox.Show("Gagal membatalkan pesanan. Coba lagi.");
            }
        }

        private void BtnDiterima_Click(object? sender, EventArgs e)
        {
            if (idTerpilih == 0)
            {
                MessageBox.Show("Klik dulu pesanan yang sudah Anda terima.");
                return;
            }
            if (statusTerpilih != "dikirim")
            {
                MessageBox.Show("Pesanan hanya bisa dikonfirmasi setelah berstatus dikirim.");
                return;
            }

            try
            {
                PesananService.Selesaikan(idTerpilih, idCustomer);
                MessageBox.Show("Terima kasih! Pesanan ditandai selesai.");
                MuatPesanan();
            }
            catch (PesananService.PesananException ex)
            {
                MessageBox.Show(ex.Message);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex);
                MessageBox.Show("Gagal memperbarui pesanan. Coba lagi.");
            }
        }
    }
}