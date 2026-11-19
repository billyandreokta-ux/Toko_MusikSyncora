using System.Data;
using System.Globalization;
using MySql.Data.MySqlClient;
using TokoMusik.Data;

namespace TokoMusik.Forms
{
    public enum TipeField { Teks, Angka, Tanggal, Pilihan, Lookup }

    public class FieldDef
    {
        public string Kolom { get; set; } = "";
        public string Label { get; set; } = "";
        public TipeField Tipe { get; set; } = TipeField.Teks;
        public bool Wajib { get; set; }
        public bool Positif { get; set; }              // khusus Angka: harus lebih dari 0
        public string[] Pilihan { get; set; } = Array.Empty<string>();
        public string LookupSql { get; set; } = "";   // contoh: SELECT id, nama FROM tabel
        public string TampilSql { get; set; } = "";   // ekspresi tampilan di tabel (opsional)
    }

    public class FormMaster : Form
    {
        private readonly string tabel;
        private readonly string kolomId;
        private readonly string kolomCari;
        private readonly List<FieldDef> fields;
        private readonly Func<Dictionary<string, object>, string?>? validasiKhusus;
        private readonly Dictionary<string, Control> input = new();
        private readonly TextBox txtCari = new();
        private readonly DataGridView grid = new();
        private int idTerpilih = 0;

        public FormMaster(string judul, string tabel, string kolomId, string kolomCari,
            List<FieldDef> fields, Func<Dictionary<string, object>, string?>? validasiKhusus = null)
        {
            this.tabel = tabel;
            this.kolomId = kolomId;
            this.kolomCari = kolomCari;
            this.fields = fields;
            this.validasiKhusus = validasiKhusus;

            Text = judul;
            StartPosition = FormStartPosition.CenterParent;
            Font = new Font("Segoe UI", 10F);

            const int lebarKolom = 420;
            int jumlahBaris = (fields.Count + 1) / 2;

            for (int i = 0; i < fields.Count; i++)
            {
                var f = fields[i];
                int x = 20 + (i % 2) * (lebarKolom + 20);
                int y = 12 + (i / 2) * 62;

                var lbl = new Label
                {
                    Text = f.Label + (f.Wajib ? " *" : ""),
                    Bounds = new Rectangle(x, y, lebarKolom, 22)
                };
                var kontrol = BuatInput(f);
                kontrol.Bounds = new Rectangle(x, y + 24, lebarKolom, 28);

                Controls.Add(lbl);
                Controls.Add(kontrol);
                input[f.Kolom] = kontrol;
            }

            int yTombol = 12 + jumlahBaris * 62 + 6;
            Controls.Add(BuatTombol("Tambah", 20, yTombol, BtnTambah_Click));
            Controls.Add(BuatTombol("Ubah", 140, yTombol, BtnUbah_Click));
            Controls.Add(BuatTombol("Hapus", 260, yTombol, BtnHapus_Click));
            Controls.Add(BuatTombol("Bersihkan", 380, yTombol, (s, e) => Bersihkan()));

            int yCari = yTombol + 50;
            Controls.Add(new Label { Text = "Cari:", Bounds = new Rectangle(20, yCari + 3, 50, 22) });
            txtCari.Bounds = new Rectangle(70, yCari, 280, 28);
            txtCari.TextChanged += (s, e) => MuatData();
            Controls.Add(txtCari);

            int yGrid = yCari + 40;
            ClientSize = new Size(900, yGrid + 270);
            MinimumSize = Size;

            grid.Bounds = new Rectangle(20, yGrid, 860, 250);
            grid.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            grid.ReadOnly = true;
            grid.AllowUserToAddRows = false;
            grid.AllowUserToDeleteRows = false;
            grid.MultiSelect = false;
            grid.RowHeadersVisible = false;
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            grid.BackgroundColor = Color.White;   // area kosong di bawah baris jadi putih
            // judul kolom panjang boleh turun ke baris berikutnya, tidak terpotong
            grid.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.True;
            grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            grid.CellClick += Grid_CellClick;
            Controls.Add(grid);

            Bersihkan();
            Load += (s, e) => MuatData();
        }

        private Button BuatTombol(string teks, int x, int y, EventHandler aksi)
        {
            var b = new Button { Text = teks, Bounds = new Rectangle(x, y, 110, 34) };
            b.Click += aksi;
            return b;
        }

        // ---------- membuat kontrol input sesuai tipe ----------
        private Control BuatInput(FieldDef f)
        {
            switch (f.Tipe)
            {
                case TipeField.Tanggal:
                    return new DateTimePicker
                    {
                        Format = DateTimePickerFormat.Short,
                        ShowCheckBox = !f.Wajib
                    };

                case TipeField.Pilihan:
                    {
                        var cb = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
                        cb.Items.AddRange(f.Pilihan);
                        if (cb.Items.Count > 0) cb.SelectedIndex = 0;
                        return cb;
                    }

                case TipeField.Lookup:
                    {
                        var cl = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
                        MuatLookup(cl, f);
                        return cl;
                    }

                default:
                    return new TextBox();
            }
        }

        private void MuatLookup(ComboBox cb, FieldDef f)
        {
            var dt = new DataTable();
            dt.Columns.Add("Id", typeof(int));
            dt.Columns.Add("Nama", typeof(string));
            if (!f.Wajib) dt.Rows.Add(0, "(tidak ada)");

            try
            {
                using var conn = Koneksi.GetConnection();
                conn.Open();
                using var cmd = new MySqlCommand(f.LookupSql, conn);
                using var r = cmd.ExecuteReader();
                while (r.Read())
                    dt.Rows.Add(r.GetInt32(0), r.GetString(1));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex);
                MessageBox.Show("Gagal memuat pilihan. Coba lagi.");
            }

            cb.DataSource = dt;
            cb.DisplayMember = "Nama";
            cb.ValueMember = "Id";
        }

        // ---------- tampil data di tabel ----------
        private void MuatData()
        {
            try
            {
                var kolomTampil = string.Join(", ", fields.Select(f =>
                    (string.IsNullOrEmpty(f.TampilSql) ? "t." + f.Kolom : f.TampilSql)
                    + " AS `" + f.Label + "`"));

                var sql = $"SELECT t.{kolomId} AS `ID`, {kolomTampil} FROM {tabel} t " +
                          $"WHERE IFNULL(t.{kolomCari}, '') LIKE @cari ORDER BY t.{kolomId} DESC";

                using var conn = Koneksi.GetConnection();
                conn.Open();
                using var da = new MySqlDataAdapter(sql, conn);
                da.SelectCommand.Parameters.AddWithValue("@cari", "%" + txtCari.Text.Trim() + "%");
                var dt = new DataTable();
                da.Fill(dt);
                grid.DataSource = dt;

                // format tampilan: tanggal dd/MM/yyyy, angka dengan pemisah ribuan
                foreach (DataGridViewColumn c in grid.Columns)
                {
                    if (c.ValueType == typeof(DateTime))
                    {
                        c.DefaultCellStyle.Format = "dd/MM/yyyy";
                    }
                    else if (c.ValueType == typeof(decimal))
                    {
                        c.DefaultCellStyle.Format = "#,##0.##";
                        c.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                    }
                }

                if (grid.Columns.Contains("ID")) grid.Columns["ID"]!.FillWeight = 25;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex);
                MessageBox.Show("Gagal memuat data. Coba lagi.");
            }
        }

        // ---------- klik baris: isi form dari database ----------
        private void Grid_CellClick(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;
            idTerpilih = Convert.ToInt32(grid.Rows[e.RowIndex].Cells["ID"].Value);

            try
            {
                using var conn = Koneksi.GetConnection();
                conn.Open();
                using var cmd = new MySqlCommand($"SELECT * FROM {tabel} WHERE {kolomId}=@id", conn);
                cmd.Parameters.AddWithValue("@id", idTerpilih);
                using var r = cmd.ExecuteReader();
                if (!r.Read()) return;

                foreach (var f in fields)
                    IsiInput(f, r[f.Kolom]);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex);
                MessageBox.Show("Gagal membaca data. Coba lagi.");
            }
        }

        private static DateTime KeTanggal(object val)
        {
            if (val is DateTime dt) return dt;
            return DateTime.Parse(val.ToString()!);
        }

        private void IsiInput(FieldDef f, object val)
        {
            bool kosong = val == DBNull.Value;
            var c = input[f.Kolom];

            switch (f.Tipe)
            {
                case TipeField.Tanggal:
                    {
                        var dtp = (DateTimePicker)c;
                        if (kosong)
                        {
                            if (dtp.ShowCheckBox) dtp.Checked = false;
                            else dtp.Value = DateTime.Today;
                        }
                        else
                        {
                            if (dtp.ShowCheckBox) dtp.Checked = true;
                            dtp.Value = KeTanggal(val);
                        }
                        break;
                    }
                case TipeField.Pilihan:
                    {
                        var cb = (ComboBox)c;
                        int idx = kosong ? -1 : cb.Items.IndexOf(val.ToString()!);
                        if (idx >= 0) cb.SelectedIndex = idx;
                        break;
                    }
                case TipeField.Lookup:
                    ((ComboBox)c).SelectedValue = kosong ? 0 : Convert.ToInt32(val);
                    break;
                case TipeField.Angka:
                    c.Text = kosong ? "" : Convert.ToDecimal(val).ToString("0.##", CultureInfo.CurrentCulture);
                    break;
                default:
                    c.Text = kosong ? "" : val.ToString();
                    break;
            }
        }

        private void Bersihkan()
        {
            idTerpilih = 0;

            foreach (var f in fields)
            {
                var c = input[f.Kolom];
                switch (f.Tipe)
                {
                    case TipeField.Tanggal:
                        {
                            var dtp = (DateTimePicker)c;
                            dtp.Value = DateTime.Today;
                            if (dtp.ShowCheckBox) dtp.Checked = false;
                            break;
                        }
                    case TipeField.Pilihan:
                    case TipeField.Lookup:
                        {
                            var cb = (ComboBox)c;
                            if (cb.Items.Count > 0) cb.SelectedIndex = 0;
                            break;
                        }
                    default:
                        c.Text = "";
                        break;
                }
            }

            grid.ClearSelection();
            if (fields.Count > 0) input[fields[0].Kolom].Focus();
        }

        // ---------- membaca dan memeriksa isi form ----------
        private bool Kumpulkan(out List<(string kolom, object nilai)> data)
        {
            data = new List<(string, object)>();

            foreach (var f in fields)
            {
                if (!AmbilNilai(f, out object nilai)) return false;
                data.Add((f.Kolom, nilai));
            }

            // aturan khusus per form (mis. promo: persen maksimal 100)
            if (validasiKhusus != null)
            {
                var kamus = data.ToDictionary(d => d.kolom, d => d.nilai);
                var pesan = validasiKhusus(kamus);
                if (pesan != null)
                {
                    MessageBox.Show(pesan);
                    return false;
                }
            }
            return true;
        }

        private bool AmbilNilai(FieldDef f, out object nilai)
        {
            nilai = DBNull.Value;
            var c = input[f.Kolom];

            switch (f.Tipe)
            {
                case TipeField.Tanggal:
                    {
                        var dtp = (DateTimePicker)c;
                        if (dtp.ShowCheckBox && !dtp.Checked) nilai = DBNull.Value;
                        else nilai = dtp.Value.Date;
                        return true;
                    }
                case TipeField.Pilihan:
                    nilai = ((ComboBox)c).SelectedItem?.ToString() ?? "";
                    return true;

                case TipeField.Lookup:
                    {
                        var sv = ((ComboBox)c).SelectedValue;
                        int id = sv == null ? 0 : Convert.ToInt32(sv);
                        if (id == 0)
                        {
                            if (f.Wajib)
                            {
                                MessageBox.Show(f.Label + " wajib dipilih.");
                                return false;
                            }
                            nilai = DBNull.Value;
                        }
                        else nilai = id;
                        return true;
                    }
                case TipeField.Angka:
                    {
                        var t = c.Text.Trim();
                        if (t == "")
                        {
                            if (f.Wajib)
                            {
                                MessageBox.Show(f.Label + " wajib diisi.");
                                return false;
                            }
                            nilai = DBNull.Value;
                            return true;
                        }
                        // hanya angka dan satu tanda desimal, tanpa titik ribuan
                        if (!decimal.TryParse(t, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,
                                CultureInfo.CurrentCulture, out var angka))
                        {
                            MessageBox.Show(f.Label + " harus berupa angka (tanpa titik pemisah ribuan).");
                            return false;
                        }
                        if (angka < 0)
                        {
                            MessageBox.Show(f.Label + " tidak boleh negatif.");
                            return false;
                        }
                        if (f.Positif && angka == 0)
                        {
                            MessageBox.Show(f.Label + " harus lebih dari 0.");
                            return false;
                        }
                        nilai = angka;
                        return true;
                    }
                default:
                    {
                        var s = c.Text.Trim();
                        if (s == "")
                        {
                            if (f.Wajib)
                            {
                                MessageBox.Show(f.Label + " wajib diisi.");
                                return false;
                            }
                            nilai = DBNull.Value;
                        }
                        else nilai = s;
                        return true;
                    }
            }
        }

        // ---------- tambah, ubah, hapus ----------
        private void BtnTambah_Click(object? sender, EventArgs e)
        {
            if (!Kumpulkan(out var data)) return;

            try
            {
                var kolom = string.Join(", ", data.Select(d => d.kolom));
                var param = string.Join(", ", data.Select((d, i) => "@p" + i));

                using var conn = Koneksi.GetConnection();
                conn.Open();
                using var cmd = new MySqlCommand($"INSERT INTO {tabel} ({kolom}) VALUES ({param})", conn);
                for (int i = 0; i < data.Count; i++)
                    cmd.Parameters.AddWithValue("@p" + i, data[i].nilai);
                cmd.ExecuteNonQuery();

                MessageBox.Show("Data berhasil ditambahkan.");
                Bersihkan();
                MuatData();
            }
            catch (MySqlException ex) when (ex.Number == 1062)
            {
                MessageBox.Show("Data dengan nilai tersebut sudah ada (nama atau kode kembar).");
            }
            catch (MySqlException ex) when (ex.Number == 3819)
            {
                MessageBox.Show("Data ditolak database: ada nilai yang tidak sesuai aturan (periksa angka dan tanggal).");
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
            if (!Kumpulkan(out var data)) return;

            try
            {
                var set = string.Join(", ", data.Select((d, i) => $"{d.kolom}=@p{i}"));

                using var conn = Koneksi.GetConnection();
                conn.Open();
                using var cmd = new MySqlCommand($"UPDATE {tabel} SET {set} WHERE {kolomId}=@id", conn);
                for (int i = 0; i < data.Count; i++)
                    cmd.Parameters.AddWithValue("@p" + i, data[i].nilai);
                cmd.Parameters.AddWithValue("@id", idTerpilih);
                cmd.ExecuteNonQuery();

                MessageBox.Show("Data berhasil diubah.");
                Bersihkan();
                MuatData();
            }
            catch (MySqlException ex) when (ex.Number == 1062)
            {
                MessageBox.Show("Nilai tersebut sudah dipakai data lain (nama atau kode kembar).");
            }
            catch (MySqlException ex) when (ex.Number == 3819)
            {
                MessageBox.Show("Data ditolak database: ada nilai yang tidak sesuai aturan (periksa angka dan tanggal).");
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

            var yakin = MessageBox.Show("Yakin hapus data ini?", "Konfirmasi",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (yakin != DialogResult.Yes) return;

            try
            {
                using var conn = Koneksi.GetConnection();
                conn.Open();
                using var cmd = new MySqlCommand($"DELETE FROM {tabel} WHERE {kolomId}=@id", conn);
                cmd.Parameters.AddWithValue("@id", idTerpilih);
                cmd.ExecuteNonQuery();

                MessageBox.Show("Data berhasil dihapus.");
                Bersihkan();
                MuatData();
            }
            catch (MySqlException ex) when (ex.Number == 1451)
            {
                MessageBox.Show("Data ini tidak bisa dihapus karena masih dipakai data lain.");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex);
                MessageBox.Show("Gagal menghapus data. Coba lagi.");
            }
        }
    }
}