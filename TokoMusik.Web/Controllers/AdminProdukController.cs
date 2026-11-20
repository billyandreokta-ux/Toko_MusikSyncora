
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MySqlConnector;
using TokoMusik.Web.Data;
using TokoMusik.Web.Models;

namespace TokoMusik.Web.Controllers
{
    [Authorize(Roles = "admin")]
    public class AdminProdukController : Controller
    {
        private readonly Koneksi _koneksi;
        private readonly IWebHostEnvironment _environment;

        private const string FolderFoto = "uploads/produk";
        private const long MaksimalUkuranFoto = 5 * 1024 * 1024;

        public AdminProdukController(
            Koneksi koneksi,
            IWebHostEnvironment environment)
        {
            _koneksi = koneksi;
            _environment = environment;
        }

        public async Task<IActionResult> Index(string? cari)
        {
            using var connection = _koneksi.GetConnection();
            await connection.OpenAsync();

            var sql = @"
                SELECT
                    p.id_produk AS IdProduk,
                    p.kode_produk AS KodeProduk,
                    p.nama_produk AS NamaProduk,
                    c.nama_category AS NamaCategory,
                    m.nama_merk AS NamaMerk,
                    p.harga_jual AS HargaJual,
                    p.foto AS Foto,
                    p.status AS Status,
                    COALESCE(s.jumlah, 0) AS JumlahStok,
                    COALESCE(s.stok_minimum, 0) AS StokMinimum
                FROM produk p
                LEFT JOIN category c
                    ON p.id_category = c.id_category
                LEFT JOIN merk m
                    ON p.id_merk = m.id_merk
                LEFT JOIN stock s
                    ON p.id_produk = s.id_produk
                WHERE
                    (@Cari IS NULL
                     OR p.nama_produk LIKE @Pola
                     OR p.kode_produk LIKE @Pola)
                ORDER BY p.id_produk DESC";

            var daftarProduk = (await connection.QueryAsync<AdminProdukItemViewModel>(
                sql,
                new
                {
                    Cari = string.IsNullOrWhiteSpace(cari) ? null : cari.Trim(),
                    Pola = $"%{cari?.Trim()}%"
                })).ToList();

            var model = new AdminProdukViewModel
            {
                Produk = daftarProduk,
                TotalProduk = daftarProduk.Count,
                TotalStokMenipis = daftarProduk.Count(x =>
                    x.JumlahStok <= x.StokMinimum),
                KataKunci = cari
            };

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> Tambah()
        {
            var model = new AdminProdukFormViewModel();
            await IsiPilihanAsync(model);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Tambah(AdminProdukFormViewModel model)
        {
            if (!ModelState.IsValid)
            {
                await IsiPilihanAsync(model);
                return View(model);
            }

            if (model.FotoFile != null)
            {
                if (!ValidasiFoto(model.FotoFile))
                {
                    ModelState.AddModelError(
                        nameof(model.FotoFile),
                        "Foto harus JPG, JPEG, PNG, atau WEBP dengan ukuran maksimal 5 MB.");

                    await IsiPilihanAsync(model);
                    return View(model);
                }
            }

            string? namaFoto = null;

            try
            {
                if (model.FotoFile != null)
                {
                    namaFoto = await SimpanFotoAsync(model.FotoFile);
                }

                using var connection = _koneksi.GetConnection();
                await connection.OpenAsync();

                using var transaction = await connection.BeginTransactionAsync();

                var sqlProduk = @"
                    INSERT INTO produk
                    (
                        id_category,
                        id_merk,
                        kode_produk,
                        nama_produk,
                        harga_jual,
                        deskripsi,
                        foto,
                        status
                    )
                    VALUES
                    (
                        @IdCategory,
                        @IdMerk,
                        @KodeProduk,
                        @NamaProduk,
                        @HargaJual,
                        @Deskripsi,
                        @Foto,
                        'aktif'
                    )";

                await connection.ExecuteAsync(
                    sqlProduk,
                    new
                    {
                        model.IdCategory,
                        model.IdMerk,
                        model.KodeProduk,
                        model.NamaProduk,
                        model.HargaJual,
                        model.Deskripsi,
                        Foto = namaFoto
                    },
                    transaction);

                var idProduk = await connection.ExecuteScalarAsync<int>(
                    "SELECT LAST_INSERT_ID()",
                    transaction: transaction);

                await connection.ExecuteAsync(
                    @"INSERT INTO stock (id_produk, jumlah, stok_minimum)
                      VALUES (@IdProduk, @Jumlah, @StokMinimum)",
                    new
                    {
                        IdProduk = idProduk,
                        Jumlah = model.JumlahStok,
                        model.StokMinimum
                    },
                    transaction);

                await transaction.CommitAsync();

                TempData["Sukses"] = "Produk berhasil ditambahkan.";
                return RedirectToAction(nameof(Index));
            }
            catch (MySqlException ex)
            {
                if (namaFoto != null)
                    HapusFoto(namaFoto);

                ModelState.AddModelError(
                    "",
                    ex.Number == 1062
                        ? "Kode produk sudah digunakan. Gunakan kode yang berbeda."
                        : "Terjadi kesalahan saat menyimpan produk.");

                await IsiPilihanAsync(model);
                return View(model);
            }
            catch
            {
                if (namaFoto != null)
                    HapusFoto(namaFoto);

                ModelState.AddModelError(
                    "",
                    "Produk gagal disimpan. Periksa kembali data dan koneksi database.");

                await IsiPilihanAsync(model);
                return View(model);
            }
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            using var connection = _koneksi.GetConnection();
            await connection.OpenAsync();

            var model = await connection.QuerySingleOrDefaultAsync<AdminProdukFormViewModel>(
                @"SELECT
                      p.id_produk AS IdProduk,
                      p.kode_produk AS KodeProduk,
                      p.nama_produk AS NamaProduk,
                      p.id_category AS IdCategory,
                      p.id_merk AS IdMerk,
                      p.harga_jual AS HargaJual,
                      p.deskripsi AS Deskripsi,
                      p.foto AS Foto,
                      COALESCE(s.jumlah, 0) AS JumlahStok,
                      COALESCE(s.stok_minimum, 0) AS StokMinimum
                  FROM produk p
                  LEFT JOIN stock s ON p.id_produk = s.id_produk
                  WHERE p.id_produk = @Id",
                new { Id = id });

            if (model == null)
                return NotFound();

            await IsiPilihanAsync(model);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(AdminProdukFormViewModel model)
        {
            if (!ModelState.IsValid)
            {
                await IsiPilihanAsync(model);
                return View(model);
            }

            if (model.FotoFile != null && !ValidasiFoto(model.FotoFile))
            {
                ModelState.AddModelError(
                    nameof(model.FotoFile),
                    "Foto harus JPG, JPEG, PNG, atau WEBP dengan ukuran maksimal 5 MB.");

                await IsiPilihanAsync(model);
                return View(model);
            }

            string? fotoBaru = null;

            try
            {
                using var connection = _koneksi.GetConnection();
                await connection.OpenAsync();

                var fotoLama = await connection.QuerySingleOrDefaultAsync<string?>(
                    "SELECT foto FROM produk WHERE id_produk = @Id",
                    new { Id = model.IdProduk });

                if (fotoLama == null && !await ProdukAdaAsync(connection, model.IdProduk))
                    return NotFound();

                if (model.FotoFile != null)
                {
                    fotoBaru = await SimpanFotoAsync(model.FotoFile);
                }

                var fotoYangDisimpan = fotoBaru ?? fotoLama;

                using var transaction = await connection.BeginTransactionAsync();

                await connection.ExecuteAsync(
                    @"UPDATE produk
                      SET id_category = @IdCategory,
                          id_merk = @IdMerk,
                          kode_produk = @KodeProduk,
                          nama_produk = @NamaProduk,
                          harga_jual = @HargaJual,
                          deskripsi = @Deskripsi,
                          foto = @Foto
                      WHERE id_produk = @IdProduk",
                    new
                    {
                        model.IdCategory,
                        model.IdMerk,
                        model.KodeProduk,
                        model.NamaProduk,
                        model.HargaJual,
                        model.Deskripsi,
                        Foto = fotoYangDisimpan,
                        model.IdProduk
                    },
                    transaction);

                var jumlahStok = await connection.ExecuteAsync(
                    @"UPDATE stock
                      SET jumlah = @JumlahStok,
                          stok_minimum = @StokMinimum
                      WHERE id_produk = @IdProduk",
                    new
                    {
                        model.JumlahStok,
                        model.StokMinimum,
                        model.IdProduk
                    },
                    transaction);

                if (jumlahStok == 0)
                {
                    await connection.ExecuteAsync(
                        @"INSERT INTO stock (id_produk, jumlah, stok_minimum)
                          VALUES (@IdProduk, @JumlahStok, @StokMinimum)",
                        new
                        {
                            model.IdProduk,
                            model.JumlahStok,
                            model.StokMinimum
                        },
                        transaction);
                }

                await transaction.CommitAsync();

                if (fotoBaru != null && !string.IsNullOrWhiteSpace(fotoLama))
                    HapusFoto(fotoLama);

                TempData["Sukses"] = "Produk berhasil diperbarui.";
                return RedirectToAction(nameof(Index));
            }
            catch (MySqlException ex)
            {
                if (fotoBaru != null)
                    HapusFoto(fotoBaru);

                ModelState.AddModelError(
                    "",
                    ex.Number == 1062
                        ? "Kode produk sudah digunakan produk lain."
                        : "Terjadi kesalahan database saat memperbarui produk.");

                await IsiPilihanAsync(model);
                return View(model);
            }
            catch
            {
                if (fotoBaru != null)
                    HapusFoto(fotoBaru);

                ModelState.AddModelError(
                    "",
                    "Produk gagal diperbarui.");

                await IsiPilihanAsync(model);
                return View(model);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Nonaktifkan(int id)
        {
            using var connection = _koneksi.GetConnection();
            await connection.OpenAsync();

            var affected = await connection.ExecuteAsync(
                @"UPDATE produk
                  SET status = 'nonaktif'
                  WHERE id_produk = @Id",
                new { Id = id });

            TempData[affected > 0 ? "Sukses" : "Gagal"] =
                affected > 0
                    ? "Produk berhasil dinonaktifkan."
                    : "Produk tidak ditemukan.";

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Aktifkan(int id)
        {
            using var connection = _koneksi.GetConnection();
            await connection.OpenAsync();

            var affected = await connection.ExecuteAsync(
                @"UPDATE produk
                  SET status = 'aktif'
                  WHERE id_produk = @Id",
                new { Id = id });

            TempData[affected > 0 ? "Sukses" : "Gagal"] =
                affected > 0
                    ? "Produk berhasil diaktifkan kembali."
                    : "Produk tidak ditemukan.";

            return RedirectToAction(nameof(Index));
        }

        private async Task IsiPilihanAsync(AdminProdukFormViewModel model)
        {
            using var connection = _koneksi.GetConnection();
            await connection.OpenAsync();

            model.Kategori = (await connection.QueryAsync<PilihanProdukViewModel>(
                @"SELECT
                      id_category AS Id,
                      nama_category AS Nama
                  FROM category
                  ORDER BY nama_category")).ToList();

            model.Merek = (await connection.QueryAsync<PilihanProdukViewModel>(
                @"SELECT
                      id_merk AS Id,
                      nama_merk AS Nama
                  FROM merk
                  ORDER BY nama_merk")).ToList();
        }

        private static bool ValidasiFoto(IFormFile foto)
        {
            if (foto.Length <= 0 || foto.Length > MaksimalUkuranFoto)
                return false;

            var extension = Path.GetExtension(foto.FileName).ToLowerInvariant();

            return extension is ".jpg" or ".jpeg" or ".png" or ".webp";
        }

        private async Task<string> SimpanFotoAsync(IFormFile foto)
        {
            var extension = Path.GetExtension(foto.FileName).ToLowerInvariant();

            var namaFile = $"{Guid.NewGuid():N}{extension}";

            var folder = Path.Combine(
                _environment.WebRootPath,
                FolderFoto.Replace('/', Path.DirectorySeparatorChar));

            Directory.CreateDirectory(folder);

            var path = Path.Combine(folder, namaFile);

            await using var stream = new FileStream(path, FileMode.Create);
            await foto.CopyToAsync(stream);

            return namaFile;
        }

        private void HapusFoto(string namaFile)
        {
            var namaAman = Path.GetFileName(namaFile);

            var path = Path.Combine(
                _environment.WebRootPath,
                FolderFoto.Replace('/', Path.DirectorySeparatorChar),
                namaAman);

            if (System.IO.File.Exists(path))
                System.IO.File.Delete(path);
        }

        private static async Task<bool> ProdukAdaAsync(
            MySqlConnection connection,
            int id)
        {
            var jumlah = await connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM produk WHERE id_produk = @Id",
                new { Id = id });

            return jumlah > 0;
        }
    }
}