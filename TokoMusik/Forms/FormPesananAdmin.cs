using System.Data;
using System.Text;
using MySql.Data.MySqlClient;
using TokoMusik.Data;

namespace TokoMusik.Forms
{
    public class FormPesananAdmin : Form
    {
        private readonly ComboBox cbStatus = new();
        private readonly TextBox txtCari = new();
        private readonly DataGridView gridPesanan = new();
        private readonly DataGridView gridDetail = new();
        private readonly TextBox txtInfo = new();
        private readonly TextBox txtKurir = new();
        private readonly TextBox txtResi = new();
        private readonly DateTimePicker dtpKirim = new();

        private int idTerpilih = 0;

        public FormPesananAdmin()
        {
            Text = "Kelola Pesanan";
            ClientSize = new Size(1100, 660);
            MinimumSize = new Size(1000, 640);
            StartPosition = FormStartPosition.CenterParent;
            Font = new Font("Segoe UI", 10F);

            txtKurir.MaxLength = 50;
            txtResi.MaxLength = 50;
            dtpKirim.Format = DateTimePickerFormat.Short;

            // ---------- filter ----------
            Controls.Add(new Label { Text = "Status:", Bounds = new Rectangle(20, 16, 55, 24) });
            cbStatus.Bounds = new Rectangle(78, 12, 170, 28);
            cbStatus.DropDownStyle = ComboBoxStyle.DropDownList;
            cbStatus.Items.AddRange(new object[]
            {
                "Semua", "menunggu_bayar", "dibayar", "diproses", "dikirim", "selesai", "dibatalkan"
            });
            cbStatus.SelectedIndex = 0;
            Controls.Add(cbStatus);

            Controls.Add(new Label { Text = "Cari (no. pesanan / customer):", Bounds = new Rectangle(270, 16, 210, 24) });
            txtCari.Bounds = new Rectangle(485, 12, 240, 28);
            Controls.Add(txtCari);

            Controls.Add(BuatTombol("Refresh", 745, 10, 100, Color.Empty, (s, e) => MuatPesanan(idTerpilih)));

            // ---------- tabel pesanan ----------
            SiapkanGrid(gridPesanan, new Rectangle(20, 50, 1060, 220), AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right);
            gridPesanan.CellClick += (s, e) =>
            {
                if (e.RowIndex >= 0) PilihPesanan(gridPesanan.Rows[e.RowIndex]);
            };

            // ---------- rincian barang dan info ----------
            Controls.Add(new Label
            {
                Text = "Rincian Barang",
                Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
                Bounds = new Rectangle(20, 278, 300, 24)
            });
            SiapkanGrid(gridDetail, new Rectangle(20, 304, 560, 190), AnchorStyles.Top | AnchorStyles.Left);

            Controls.Add(new Label
            {
                Text = "Info Pesanan",
                Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
                Bounds = new Rectangle(600, 278, 300, 24)
            });
            txtInfo.Bounds = new Rectangle(600, 304, 480, 190);
            txtInfo.Multiline = true;
            txtInfo.ReadOnly = true;
            txtInfo.ScrollBars = ScrollBars.Vertical;
            txtInfo.BackColor = Color.White;
            txtInfo.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            Controls.Add(txtInfo);

            // ---------- data pengiriman ----------
            Controls.Add(new Label
            {
                Text = "Data pengiriman (diisi saat menekan \"Kirim Pesanan\")",
                Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
                Bounds = new Rectangle(20, 502, 500, 24)
            });
            Controls.Add(new Label { Text = "Kurir", Bounds = new Rectangle(20, 528, 200, 20) });
            txtKurir.Bounds = new Rectangle(20, 550, 220, 27);
            Controls.Add(txtKurir);
            Controls.Add(new Label { Text = "No. Resi", Bounds = new Rectangle(260, 528, 200, 20) });
            txtResi.Bounds = new Rectangle(260, 550, 240, 27);
            Controls.Add(txtResi);
            Controls.Add(new Label { Text = "Tanggal Kirim", Bounds = new Rectangle(520, 528, 160, 20) });
            dtpKirim.Bounds = new Rectangle(520, 550, 160, 27);
            Controls.Add(dtpKirim);

            // ---------- tombol aksi ----------
            Controls.Add(BuatTombol("Verifikasi Pembayaran", 20, 596, 200, Color.SeaGreen, BtnVerifikasi_Click));
            Controls.Add(BuatTombol("Proses Pesanan", 230, 596, 150, Color.Empty, BtnProses_Click));
            Controls.Add(BuatTombol("Kirim Pesanan", 390, 596, 140, Color.Empty, BtnKirim_Click));
            Controls.Add(BuatTombol("Tandai Selesai", 540, 596, 140, Color.Empty, BtnSelesai_Click));
            Controls.Add(BuatTombol("Batalkan Pesanan", 690, 596, 160, Color.Firebrick, BtnBatal_Click));

            // filter dipasang setelah isinya siap
            cbStatus.SelectedIndexChanged += (s, e) => MuatPesanan();
            txtCari.TextChanged += (s, e) => MuatPesanan();

            Load += (s, e) => MuatPesanan();
        }

        // ================= tata letak =================
        private void SiapkanGrid(DataGridView g, Rectangle posisi, AnchorStyles anchor)
        {
            g.Bounds = posisi;
            g.Anchor = anchor;
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

        private static Button BuatTombol(string teks, int x, int y, int lebar, Color warna, EventHandler aksi)
        {
            var b = new Button { Text = teks, Bounds = new Rectangle(x, y, lebar, 38) };
            if (warna != Color.Empty)
            {
                b.FlatStyle = FlatStyle.Flat;
                b.BackColor = warna;
                b.ForeColor = Color.White;
                b.FlatAppearance.BorderSize = 0;
            }
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

        private static string S(object v) => v == DBNull.Value ? "" : v.ToString() ?? "";

        // ================= daftar pesanan =================
        private void MuatPesanan(int pilihId = 0)
        {
            try
            {
                string st = cbStatus.SelectedItem?.ToString() ?? "Semua";

                using var conn = Koneksi.GetConnection();
                conn.Open();
                using var da = new MySqlDataAdapter(
                    "SELECT p.id_pemesanan AS ID, p.tgl_pesan AS Tanggal, c.nama_customer AS Customer, " +
                    "p.total_bayar AS Total, p.status AS Status, " +
                    "IFNULL((SELECT py.status FROM payments py WHERE py.id_pemesanan = p.id_pemesanan " +
                    "ORDER BY py.id_payment DESC LIMIT 1), '-') AS Pembayaran, " +
                    "IFNULL((SELECT pg.status FROM pengiriman pg WHERE pg.id_pemesanan = p.id_pemesanan), '-') AS Pengiriman " +
                    "FROM pemesanan p JOIN customer c ON c.id_customer = p.id_customer " +
                    "WHERE (@st = '' OR p.status = @st) " +
                    "AND (CAST(p.id_pemesanan AS CHAR) LIKE @cari OR c.nama_customer LIKE @cari) " +
                    "ORDER BY p.id_pemesanan DESC", conn);
                da.SelectCommand.Parameters.AddWithValue("@st", st == "Semua" ? "" : st);
                da.SelectCommand.Parameters.AddWithValue("@cari", "%" + txtCari.Text.Trim() + "%");
                var dt = new DataTable();
                da.Fill(dt);
                gridPesanan.DataSource = dt;

                if (gridPesanan.Columns.Contains("ID")) gridPesanan.Columns["ID"]!.FillWeight = 20;
                if (gridPesanan.Columns.Contains("Tanggal"))
                    gridPesanan.Columns["Tanggal"]!.DefaultCellStyle.Format = "dd/MM/yyyy HH:mm";
                FormatUang(gridPesanan, "Total");

                gridPesanan.ClearSelection();
                KosongkanDetail();

                // pilih lagi pesanan yang tadi sedang dibuka
                if (pilihId > 0)
                {
                    foreach (DataGridViewRow row in gridPesanan.Rows)
                    {
                        if (Convert.ToInt32(row.Cells["ID"].Value) != pilihId) continue;
                        gridPesanan.CurrentCell = row.Cells["ID"];
                        row.Selected = true;
                        PilihPesanan(row);
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex);
                MessageBox.Show("Gagal memuat pesanan. Coba lagi.");
            }
        }

        private void KosongkanDetail()
        {
            idTerpilih = 0;
            gridDetail.DataSource = null;
            txtInfo.Clear();
            txtKurir.Clear();
            txtResi.Clear();
            dtpKirim.Value = DateTime.Today;
        }

        private void PilihPesanan(DataGridViewRow row)
        {
            idTerpilih = Convert.ToInt32(row.Cells["ID"].Value);
            MuatDetail();
            MuatInfo();
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

        private void MuatInfo()
        {
            try
            {
                using var conn = Koneksi.GetConnection();
                conn.Open();
                using var cmd = new MySqlCommand(
                    "SELECT p.subtotal, p.diskon, p.ongkir, p.total_bayar, p.status AS o_status, " +
                    "a.penerima, a.telepon, a.alamat_lengkap, a.kota, a.provinsi, a.kode_pos, " +
                    "pr.kode_promo, mp.nama_metode, py.status AS pay_status, py.jumlah_bayar, py.tgl_bayar, " +
                    "pg.kurir, pg.no_resi, pg.tgl_kirim, pg.tgl_terima, pg.status AS pg_status " +
                    "FROM pemesanan p " +
                    "LEFT JOIN alamat_customer a ON a.id_alamat = p.id_alamat " +
                    "LEFT JOIN promo pr ON pr.id_promo = p.id_promo " +
                    "LEFT JOIN payments py ON py.id_payment = " +
                    "(SELECT MAX(x.id_payment) FROM payments x WHERE x.id_pemesanan = p.id_pemesanan) " +
                    "LEFT JOIN metode_pembayaran mp ON mp.id_metode = py.id_metode " +
                    "LEFT JOIN pengiriman pg ON pg.id_pemesanan = p.id_pemesanan " +
                    "WHERE p.id_pemesanan = @id", conn);
                cmd.Parameters.AddWithValue("@id", idTerpilih);
                using var r = cmd.ExecuteReader();
                if (!r.Read()) return;

                var sb = new StringBuilder();
                sb.AppendLine("Pesanan #" + idTerpilih + "  (" + S(r["o_status"]) + ")");
                sb.AppendLine();

                sb.AppendLine("PENERIMA");
                if (r["penerima"] == DBNull.Value)
                {
                    sb.AppendLine("(alamat sudah dihapus oleh customer)");
                }
                else
                {
                    sb.AppendLine(S(r["penerima"]) + (S(r["telepon"]) != "" ? " - " + S(r["telepon"]) : ""));
                    sb.AppendLine(S(r["alamat_lengkap"]));
                    var wilayah = string.Join(", ", new[] { S(r["kota"]), S(r["provinsi"]), S(r["kode_pos"]) }
                        .Where(x => x != ""));
                    if (wilayah != "") sb.AppendLine(wilayah);
                }
                sb.AppendLine();

                sb.AppendLine("BIAYA");
                sb.AppendLine("Subtotal: Rp " + Convert.ToDecimal(r["subtotal"]).ToString("N0"));
                sb.AppendLine("Diskon" + (S(r["kode_promo"]) != "" ? " (" + S(r["kode_promo"]) + ")" : "") +
                              ": Rp " + Convert.ToDecimal(r["diskon"]).ToString("N0"));
                sb.AppendLine("Ongkir: Rp " + Convert.ToDecimal(r["ongkir"]).ToString("N0"));
                sb.AppendLine("Total: Rp " + Convert.ToDecimal(r["total_bayar"]).ToString("N0"));
                sb.AppendLine();

                sb.AppendLine("PEMBAYARAN");
                if (r["pay_status"] == DBNull.Value)
                {
                    sb.AppendLine("(belum ada data pembayaran)");
                }
                else
                {
                    sb.AppendLine("Metode: " + S(r["nama_metode"]));
                    sb.AppendLine("Status: " + S(r["pay_status"]));
                    if (r["tgl_bayar"] != DBNull.Value && S(r["pay_status"]) == "berhasil")
                        sb.AppendLine("Diterima: " + Convert.ToDateTime(r["tgl_bayar"]).ToString("dd/MM/yyyy HH:mm"));
                }
                sb.AppendLine();

                sb.AppendLine("PENGIRIMAN");
                if (r["pg_status"] == DBNull.Value)
                {
                    sb.AppendLine("(belum dikirim)");
                }
                else
                {
                    sb.AppendLine("Kurir: " + S(r["kurir"]) + "   Resi: " + S(r["no_resi"]));
                    sb.AppendLine("Status: " + S(r["pg_status"]));
                    if (r["tgl_kirim"] != DBNull.Value)
                        sb.AppendLine("Dikirim: " + Convert.ToDateTime(r["tgl_kirim"]).ToString("dd/MM/yyyy"));
                    if (r["tgl_terima"] != DBNull.Value)
                        sb.AppendLine("Diterima: " + Convert.ToDateTime(r["tgl_terima"]).ToString("dd/MM/yyyy"));
                }
                txtInfo.Text = sb.ToString();

                // isi kolom pengiriman kalau datanya sudah ada
                txtKurir.Text = S(r["kurir"]);
                txtResi.Text = S(r["no_resi"]);
                dtpKirim.Value = r["tgl_kirim"] == DBNull.Value
                    ? DateTime.Today : Convert.ToDateTime(r["tgl_kirim"]);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex);
                MessageBox.Show("Gagal memuat info pesanan. Coba lagi.");
            }
        }

        // ================= aksi =================
        private bool Dipilih()
        {
            if (idTerpilih != 0) return true;
            MessageBox.Show("Klik dulu pesanan di tabel.");
            return false;
        }

        private void Jalankan(Func<string> aksi)
        {
            int id = idTerpilih;
            try
            {
                string pesan = aksi();
                MessageBox.Show(pesan);
            }
            catch (PesananService.PesananException ex)
            {
                MessageBox.Show(ex.Message);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex);
                MessageBox.Show("Gagal memproses pesanan. Coba lagi.");
            }
            MuatPesanan(id);
        }

        private void BtnVerifikasi_Click(object? sender, EventArgs e)
        {
            if (!Dipilih()) return;

            var yakin = MessageBox.Show(
                "Pembayaran pesanan #" + idTerpilih + " sudah diterima dan valid?", "Konfirmasi",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (yakin != DialogResult.Yes) return;

            Jalankan(() =>
            {
                AdminPesananService.VerifikasiPembayaran(idTerpilih);
                return "Pembayaran diverifikasi. Status pesanan menjadi dibayar.";
            });
        }

        private void BtnProses_Click(object? sender, EventArgs e)
        {
            if (!Dipilih()) return;

            Jalankan(() =>
            {
                AdminPesananService.Proses(idTerpilih);
                return "Pesanan sedang diproses.";
            });
        }

        private void BtnKirim_Click(object? sender, EventArgs e)
        {
            if (!Dipilih()) return;

            if (string.IsNullOrWhiteSpace(txtKurir.Text))
            {
                MessageBox.Show("Nama kurir wajib diisi.");
                return;
            }
            if (string.IsNullOrWhiteSpace(txtResi.Text))
            {
                MessageBox.Show("Nomor resi wajib diisi.");
                return;
            }

            Jalankan(() =>
            {
                AdminPesananService.Kirim(idTerpilih, txtKurir.Text, txtResi.Text, dtpKirim.Value);
                return "Pesanan ditandai dikirim.";
            });
        }

        private void BtnSelesai_Click(object? sender, EventArgs e)
        {
            if (!Dipilih()) return;

            var yakin = MessageBox.Show(
                "Tandai pesanan #" + idTerpilih + " sudah diterima customer?", "Konfirmasi",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (yakin != DialogResult.Yes) return;

            Jalankan(() =>
            {
                AdminPesananService.Selesaikan(idTerpilih);
                return "Pesanan ditandai selesai.";
            });
        }

        private void BtnBatal_Click(object? sender, EventArgs e)
        {
            if (!Dipilih()) return;

            var yakin = MessageBox.Show(
                "Yakin batalkan pesanan #" + idTerpilih + "?\nStok barang akan dikembalikan.", "Konfirmasi",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (yakin != DialogResult.Yes) return;

            Jalankan(() =>
            {
                bool sudahDibayar = AdminPesananService.Batalkan(idTerpilih);
                return sudahDibayar
                    ? "Pesanan dibatalkan dan stok dikembalikan.\nPembayaran pesanan ini sudah diterima, kembalikan dananya ke customer secara manual."
                    : "Pesanan dibatalkan dan stok dikembalikan.";
            });
        }
    }
}