namespace TokoMusik.Data
{
    public static class FotoHelper
    {
        private static readonly string[] EkstensiBoleh = { ".jpg", ".jpeg", ".png", ".gif", ".bmp" };

        // folder bersama untuk semua akun Windows di komputer ini
        public static string Folder
        {
            get
            {
                var p = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    "TokoMusik", "Foto");
                Directory.CreateDirectory(p);
                return p;
            }
        }

        // menyalin foto pilihan ke folder aplikasi, mengembalikan nama file barunya
        public static string Simpan(string sumber)
        {
            var ext = Path.GetExtension(sumber).ToLower();
            if (!EkstensiBoleh.Contains(ext))
                throw new InvalidOperationException("Format foto harus JPG, PNG, GIF, atau BMP.");

            var nama = Guid.NewGuid().ToString("N") + ext;
            File.Copy(sumber, Path.Combine(Folder, nama), true);
            return nama;
        }

        public static void Hapus(string? nama)
        {
            if (string.IsNullOrEmpty(nama)) return;
            try
            {
                // GetFileName memastikan hanya file di dalam folder foto yang bisa dihapus
                var p = Path.Combine(Folder, Path.GetFileName(nama));
                if (File.Exists(p)) File.Delete(p);
            }
            catch { }
        }

        // memuat foto dari folder aplikasi (berdasarkan nama file di database)
        public static Image? Muat(string? nama)
        {
            if (string.IsNullOrEmpty(nama)) return null;
            return MuatDariPath(Path.Combine(Folder, Path.GetFileName(nama)));
        }

        // memuat foto dari lokasi mana saja, tanpa mengunci filenya
        public static Image? MuatDariPath(string path)
        {
            if (!File.Exists(path)) return null;
            try
            {
                using var fs = new FileStream(path, FileMode.Open, FileAccess.Read);
                using var img = Image.FromStream(fs);
                return new Bitmap(img);
            }
            catch
            {
                return null;
            }
        }
    }
}