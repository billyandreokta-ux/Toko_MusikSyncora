
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
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

        private readonly string[] EkstensiFoto =
        {
            ".jpg", ".jpeg", ".png", ".webp"
        };

        public AdminProdukController(
            Koneksi koneksi,
            IWebHostEnvironment environment)
        {
            _koneksi = koneksi;
            _environment = environment;
        }

        // =====================================================
        // DAFTAR PRODUK
        // =====================================================

        [HttpGet]
        public async Task<IActionResult> Index(string? cari)
        {
            using var connection = _koneksi.GetConnection();

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
                    COALESCE(s.stok_minimum, 5) AS StokMinimum
                FROM produk p
                LEFT JOIN category c
                    ON p.id_category = c.id_category
                LEFT JOIN merk m
                    ON p.id_merk = m.id_merk
                LEFT JOIN stock s
                    ON p.id_produk = s.id_produk
                WHERE
                    (@KataKunci IS NULL
                    OR p.nama_produk LIKE CONCAT('%', @KataKunci, '%')
                    OR p.kode_produk LIKE CONCAT('%', @KataKunci, '%'))
                ORDER BY p.id_produk DESC;
            ";

            var daftarProduk = (await connection.QueryAsync<AdminProdukItemViewModel>(
                sql,
                new { KataKunci = string.IsNullOrWhiteSpace(cari) ? null : cari.Trim() }
            )).ToList();

            var model = new AdminProdukViewModel
            {
                Produk = daftarProduk,
                TotalProduk = daftarProduk.Count,
                TotalStokMenipis = daftarProduk.Count(
                    p => p.JumlahStok <= p.StokMinimum
                ),
                KataKunci = cari
            };

            return View(model);
        }

        // =====================================================
        // TAMBAH PRODUK - GET
        // =====================================================

        [HttpGet]
        public async Task<IActionResult> Tambah()
        {
            var model = new AdminProdukFormViewModel();

            await IsiPilihanAsync(model);

            return View(model);
        }

        // =====================================================
        // TAMBAH PRODUK - POST
        // =====================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Tambah(AdminProdukFormViewModel model)
        {
            await IsiPilihanAsync(model);

            if (model.FotoFile != null)
            {
                ValidasiFoto(model.FotoFile);
            }

            if (!ModelState.IsValid)
            {
                return View(model);
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

                try
                {
                    const string sqlProduk = @"
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
                        );

                        SELECT LAST_INSERT_ID();
                    ";

                    var idProduk = await connection.ExecuteScalarAsync<int>(
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
                        transaction
                    );

                    const string sqlStock = @"
                        INSERT INTO stock
                        (
                            id_produk,
                            jumlah,
                            stok_minimum
                        )
                        VALUES
                        (
                            @IdProduk,
                            @JumlahStok,
                            @StokMinimum
                        );
                    ";

                    await connection.ExecuteAsync(
                        sqlStock,
                        new
                        {
                            IdProduk = idProduk,
                            model.JumlahStok,
                            model.StokMinimum
                        },
                        transaction
                    );

                    await transaction.CommitAsync();

                    TempData["Sukses"] = "Produk berhasil ditambahkan.";

                    return RedirectToAction(nameof(Index));
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }
            catch (MySqlException ex)
            {
                if (!string.IsNullOrWhiteSpace(namaFoto))
                {
                    HapusFoto(namaFoto);
                }

                if (ex.Number == 1062)
                {
                    ModelState.AddModelError(
                        nameof(model.KodeProduk),
                        "Kode produk sudah digunakan."
                    );
                }
                else
                {
                    ModelState.AddModelError(
                        "",
                        "Terjadi kesalahan database saat menambahkan produk."
                    );
                }

                await IsiPilihanAsync(model);
                return View(model);
            }
            catch
            {
                if (!string.IsNullOrWhiteSpace(namaFoto))
                {
                    HapusFoto(namaFoto);
                }

                ModelState.AddModelError(
                    "",
                    "Produk gagal ditambahkan. Silakan periksa kembali data."
                );

                await IsiPilihanAsync(model);
                return View(model);
            }
        }

        // =====================================================
        // EDIT PRODUK - GET
        // =====================================================

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            using var connection = _koneksi.GetConnection();

            const string sql = @"
                SELECT
                    p.id_produk AS IdProduk,
                    p.kode_produk AS KodeProduk,
                    p.nama_produk AS NamaProduk,
                    p.id_category AS IdCategory,
                    p.id_merk AS IdMerk,
                    p.harga_jual AS HargaJual,
                    p.deskripsi AS Deskripsi,
                    p.foto AS Foto,
                    COALESCE(s.jumlah, 0) AS JumlahStok,
                    COALESCE(s.stok_minimum, 5) AS StokMinimum
                FROM produk p
                LEFT JOIN stock s
                    ON p.id_produk = s.id_produk
                WHERE p.id_produk = @Id;
            ";

            var model = await connection.QuerySingleOrDefaultAsync<AdminProdukFormViewModel>(
                sql,
                new { Id = id }
            );

            if (model == null)
            {
                TempData["Gagal"] = "Produk tidak ditemukan.";
                return RedirectToAction(nameof(Index));
            }

            await IsiPilihanAsync(model);

            return View(model);
        }

        // =====================================================
        // EDIT PRODUK - POST
        // =====================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(AdminProdukFormViewModel model)
        {
            await IsiPilihanAsync(model);

            if (model.FotoFile != null)
            {
                ValidasiFoto(model.FotoFile);
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            string? fotoBaru = null;
            string? fotoLama = null;

            try
            {
                using var connection = _koneksi.GetConnection();
                await connection.OpenAsync();

                const string sqlFotoLama = @"
                    SELECT foto
                    FROM produk
                    WHERE id_produk = @IdProduk;
                ";

                fotoLama = await connection.QuerySingleOrDefaultAsync<string?>(
                    sqlFotoLama,
                    new { model.IdProduk }
                );

                if (fotoLama == null)
                {
                    var jumlahProduk = await connection.ExecuteScalarAsync<int>(
                        "SELECT COUNT(*) FROM produk WHERE id_produk = @IdProduk;",
                        new { model.IdProduk }
                    );

                    if (jumlahProduk == 0)
                    {
                        TempData["Gagal"] = "Produk tidak ditemukan.";
                        return RedirectToAction(nameof(Index));
                    }
                }

                if (model.FotoFile != null)
                {
                    fotoBaru = await SimpanFotoAsync(model.FotoFile);
                }

                using var transaction = await connection.BeginTransactionAsync();

                try
                {
                    const string sqlProduk = @"
                        UPDATE produk
                        SET
                            id_category = @IdCategory,
                            id_merk = @IdMerk,
                            kode_produk = @KodeProduk,
                            nama_produk = @NamaProduk,
                            harga_jual = @HargaJual,
                            deskripsi = @Deskripsi,
                            foto = @Foto
                        WHERE id_produk = @IdProduk;
                    ";

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
                            Foto = fotoBaru ?? fotoLama,
                            model.IdProduk
                        },
                        transaction
                    );

                    const string sqlCekStock = @"
                        SELECT COUNT(*)
                        FROM stock
                        WHERE id_produk = @IdProduk;
                    ";

                    var stockAda = await connection.ExecuteScalarAsync<int>(
                        sqlCekStock,
                        new { model.IdProduk },
                        transaction
                    );

                    if (stockAda > 0)
                    {
                        const string sqlUpdateStock = @"
                            UPDATE stock
                            SET
                                jumlah = @JumlahStok,
                                stok_minimum = @StokMinimum
                            WHERE id_produk = @IdProduk;
                        ";

                        await connection.ExecuteAsync(
                            sqlUpdateStock,
                            new
                            {
                                model.JumlahStok,
                                model.StokMinimum,
                                model.IdProduk
                            },
                            transaction
                        );
                    }
                    else
                    {
                        const string sqlInsertStock = @"
                            INSERT INTO stock
                            (
                                id_produk,
                                jumlah,
                                stok_minimum
                            )
                            VALUES
                            (
                                @IdProduk,
                                @JumlahStok,
                                @StokMinimum
                            );
                        ";

                        await connection.ExecuteAsync(
                            sqlInsertStock,
                            new
                            {
                                model.IdProduk,
                                model.JumlahStok,
                                model.StokMinimum
                            },
                            transaction
                        );
                    }

                    await transaction.CommitAsync();

                    if (!string.IsNullOrWhiteSpace(fotoBaru)
                        && !string.IsNullOrWhiteSpace(fotoLama))
                    {
                        HapusFoto(fotoLama);
                    }

                    TempData["Sukses"] = "Produk berhasil diperbarui.";

                    return RedirectToAction(nameof(Index));
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }
            catch (MySqlException ex)
            {
                if (!string.IsNullOrWhiteSpace(fotoBaru))
                {
                    HapusFoto(fotoBaru);
                }

                if (ex.Number == 1062)
                {
                    ModelState.AddModelError(
                        nameof(model.KodeProduk),
                        "Kode produk sudah digunakan oleh produk lain."
                    );
                }
                else
                {
                    ModelState.AddModelError(
                        "",
                        "Terjadi kesalahan database saat memperbarui produk."
                    );
                }

                await IsiPilihanAsync(model);
                return View(model);
            }
            catch
            {
                if (!string.IsNullOrWhiteSpace(fotoBaru))
                {
                    HapusFoto(fotoBaru);
                }

                ModelState.AddModelError(
                    "",
                    "Produk gagal diperbarui. Silakan coba kembali."
                );

                await IsiPilihanAsync(model);
                return View(model);
            }
        }

        // =====================================================
        // HAPUS PRODUK (SOFT DELETE)
        // =====================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Nonaktifkan(int id)
        {
            using var connection = _koneksi.GetConnection();

            const string sql = @"
                UPDATE produk
                SET status = 'nonaktif'
                WHERE id_produk = @Id;
            ";

            var hasil = await connection.ExecuteAsync(sql, new { Id = id });

            if (hasil > 0)
            {
                TempData["Sukses"] =
                    "Produk berhasil dihapus dari katalog.";
            }
            else
            {
                TempData["Gagal"] = "Produk tidak ditemukan.";
            }

            return RedirectToAction(nameof(Index));
        }

        // =====================================================
        // AKTIFKAN KEMBALI PRODUK
        // =====================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Aktifkan(int id)
        {
            using var connection = _koneksi.GetConnection();

            const string sql = @"
                UPDATE produk
                SET status = 'aktif'
                WHERE id_produk = @Id;
            ";

            var hasil = await connection.ExecuteAsync(sql, new { Id = id });

            if (hasil > 0)
            {
                TempData["Sukses"] = "Produk berhasil diaktifkan kembali.";
            }
            else
            {
                TempData["Gagal"] = "Produk tidak ditemukan.";
            }

            return RedirectToAction(nameof(Index));
        }

        // =====================================================
        // MENGAMBIL DATA KATEGORI DAN MEREK
        // =====================================================

        private async Task IsiPilihanAsync(AdminProdukFormViewModel model)
        {
            using var connection = _koneksi.GetConnection();

            const string sqlKategori = @"
                SELECT
                    id_category AS Id,
                    nama_category AS Nama
                FROM category
                ORDER BY nama_category;
            ";

            const string sqlMerek = @"
                SELECT
                    id_merk AS Id,
                    nama_merk AS Nama
                FROM merk
                ORDER BY nama_merk;
            ";

            model.Kategori = (await connection.QueryAsync<PilihanProdukViewModel>(
                sqlKategori
            )).ToList();

            model.Merek = (await connection.QueryAsync<PilihanProdukViewModel>(
                sqlMerek
            )).ToList();
        }

        // =====================================================
        // VALIDASI FOTO
        // =====================================================

        private void ValidasiFoto(IFormFile foto)
        {
            if (foto.Length <= 0)
            {
                ModelState.AddModelError(
                    nameof(AdminProdukFormViewModel.FotoFile),
                    "File foto kosong."
                );

                return;
            }

            if (foto.Length > MaksimalUkuranFoto)
            {
                ModelState.AddModelError(
                    nameof(AdminProdukFormViewModel.FotoFile),
                    "Ukuran foto maksimal 5 MB."
                );
            }

            var ekstensi = Path.GetExtension(foto.FileName).ToLowerInvariant();

            if (!EkstensiFoto.Contains(ekstensi))
            {
                ModelState.AddModelError(
                    nameof(AdminProdukFormViewModel.FotoFile),
                    "Format foto harus JPG, JPEG, PNG, atau WEBP."
                );
            }
        }

        // =====================================================
        // MENYIMPAN FOTO
        // =====================================================

        private async Task<string> SimpanFotoAsync(IFormFile foto)
        {
            var ekstensi = Path.GetExtension(foto.FileName).ToLowerInvariant();

            var namaFile = $"{Guid.NewGuid():N}{ekstensi}";

            var folder = Path.Combine(
                _environment.WebRootPath,
                FolderFoto
            );

            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }

            var path = Path.Combine(folder, namaFile);

            using (var stream = new FileStream(path, FileMode.Create))
            {
                await foto.CopyToAsync(stream);
            }

            return namaFile;
        }

        // =====================================================
        // MENGHAPUS FILE FOTO LAMA
        // =====================================================

        private void HapusFoto(string namaFoto)
        {
            if (string.IsNullOrWhiteSpace(namaFoto))
            {
                return;
            }

            var namaFile = Path.GetFileName(namaFoto);

            if (string.IsNullOrWhiteSpace(namaFile))
            {
                return;
            }

            var folder = Path.Combine(
                _environment.WebRootPath,
                FolderFoto
            );

            var path = Path.Combine(folder, namaFile);

            if (System.IO.File.Exists(path))
            {
                System.IO.File.Delete(path);
            }
        }
    }
}
