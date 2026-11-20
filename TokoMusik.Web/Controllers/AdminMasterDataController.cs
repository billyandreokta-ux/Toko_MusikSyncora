
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TokoMusik.Web.Data;
using TokoMusik.Web.Models;

namespace TokoMusik.Web.Controllers
{
    [Authorize(Roles = "admin")]
    public class AdminMasterDataController : Controller
    {
        private readonly Koneksi _koneksi;

        public AdminMasterDataController(Koneksi koneksi)
        {
            _koneksi = koneksi;
        }

        // =========================
        // KATEGORI
        // =========================

        [HttpGet]
        public async Task<IActionResult> Kategori()
        {
            var model = await AmbilDaftarAsync("kategori");
            return View(model);
        }

        [HttpGet]
        public IActionResult TambahKategori()
        {
            return View("FormKategori", new MasterDataFormViewModel
            {
                Jenis = "kategori"
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TambahKategori(MasterDataFormViewModel model)
        {
            model.Jenis = "kategori";

            if (!ModelState.IsValid)
                return View("FormKategori", model);

            using var connection = _koneksi.GetConnection();
            await connection.OpenAsync();

            var sudahAda = await connection.ExecuteScalarAsync<int>(
                @"SELECT COUNT(*)
                  FROM category
                  WHERE LOWER(nama_category) = LOWER(@Nama)",
                new { Nama = model.Nama.Trim() });

            if (sudahAda > 0)
            {
                ModelState.AddModelError(nameof(model.Nama), "Nama kategori sudah digunakan.");
                return View("FormKategori", model);
            }

            await connection.ExecuteAsync(
                @"INSERT INTO category (nama_category)
                  VALUES (@Nama)",
                new { Nama = model.Nama.Trim() });

            TempData["Sukses"] = "Kategori berhasil ditambahkan.";
            return RedirectToAction(nameof(Kategori));
        }

        [HttpGet]
        public async Task<IActionResult> EditKategori(int id)
        {
            using var connection = _koneksi.GetConnection();
            await connection.OpenAsync();

            var model = await connection.QuerySingleOrDefaultAsync<MasterDataFormViewModel>(
                @"SELECT
                      id_category AS Id,
                      nama_category AS Nama
                  FROM category
                  WHERE id_category = @Id",
                new { Id = id });

            if (model == null)
                return NotFound();

            model.Jenis = "kategori";
            return View("FormKategori", model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditKategori(MasterDataFormViewModel model)
        {
            model.Jenis = "kategori";

            if (!ModelState.IsValid)
                return View("FormKategori", model);

            using var connection = _koneksi.GetConnection();
            await connection.OpenAsync();

            var sudahAda = await connection.ExecuteScalarAsync<int>(
                @"SELECT COUNT(*)
                  FROM category
                  WHERE LOWER(nama_category) = LOWER(@Nama)
                    AND id_category <> @Id",
                new { Nama = model.Nama.Trim(), model.Id });

            if (sudahAda > 0)
            {
                ModelState.AddModelError(nameof(model.Nama), "Nama kategori sudah digunakan.");
                return View("FormKategori", model);
            }

            var affected = await connection.ExecuteAsync(
                @"UPDATE category
                  SET nama_category = @Nama
                  WHERE id_category = @Id",
                new { Nama = model.Nama.Trim(), model.Id });

            if (affected == 0)
                return NotFound();

            TempData["Sukses"] = "Kategori berhasil diperbarui.";
            return RedirectToAction(nameof(Kategori));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> HapusKategori(int id)
        {
            using var connection = _koneksi.GetConnection();
            await connection.OpenAsync();

            var digunakan = await connection.ExecuteScalarAsync<int>(
                @"SELECT COUNT(*)
                  FROM produk
                  WHERE id_category = @Id",
                new { Id = id });

            if (digunakan > 0)
            {
                TempData["Gagal"] =
                    "Kategori tidak dapat dihapus karena masih digunakan oleh produk.";
                return RedirectToAction(nameof(Kategori));
            }

            var affected = await connection.ExecuteAsync(
                "DELETE FROM category WHERE id_category = @Id",
                new { Id = id });

            TempData[affected > 0 ? "Sukses" : "Gagal"] =
                affected > 0
                    ? "Kategori berhasil dihapus."
                    : "Kategori tidak ditemukan.";

            return RedirectToAction(nameof(Kategori));
        }

        // =========================
        // MEREK
        // =========================

        [HttpGet]
        public async Task<IActionResult> Merek()
        {
            var model = await AmbilDaftarAsync("merek");
            return View(model);
        }

        [HttpGet]
        public IActionResult TambahMerek()
        {
            return View("FormMerek", new MasterDataFormViewModel
            {
                Jenis = "merek"
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TambahMerek(MasterDataFormViewModel model)
        {
            model.Jenis = "merek";

            if (!ModelState.IsValid)
                return View("FormMerek", model);

            using var connection = _koneksi.GetConnection();
            await connection.OpenAsync();

            var sudahAda = await connection.ExecuteScalarAsync<int>(
                @"SELECT COUNT(*)
                  FROM merk
                  WHERE LOWER(nama_merk) = LOWER(@Nama)",
                new { Nama = model.Nama.Trim() });

            if (sudahAda > 0)
            {
                ModelState.AddModelError(nameof(model.Nama), "Nama merek sudah digunakan.");
                return View("FormMerek", model);
            }

            await connection.ExecuteAsync(
                @"INSERT INTO merk (nama_merk)
                  VALUES (@Nama)",
                new { Nama = model.Nama.Trim() });

            TempData["Sukses"] = "Merek berhasil ditambahkan.";
            return RedirectToAction(nameof(Merek));
        }

        [HttpGet]
        public async Task<IActionResult> EditMerek(int id)
        {
            using var connection = _koneksi.GetConnection();
            await connection.OpenAsync();

            var model = await connection.QuerySingleOrDefaultAsync<MasterDataFormViewModel>(
                @"SELECT
                      id_merk AS Id,
                      nama_merk AS Nama
                  FROM merk
                  WHERE id_merk = @Id",
                new { Id = id });

            if (model == null)
                return NotFound();

            model.Jenis = "merek";
            return View("FormMerek", model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditMerek(MasterDataFormViewModel model)
        {
            model.Jenis = "merek";

            if (!ModelState.IsValid)
                return View("FormMerek", model);

            using var connection = _koneksi.GetConnection();
            await connection.OpenAsync();

            var sudahAda = await connection.ExecuteScalarAsync<int>(
                @"SELECT COUNT(*)
                  FROM merk
                  WHERE LOWER(nama_merk) = LOWER(@Nama)
                    AND id_merk <> @Id",
                new { Nama = model.Nama.Trim(), model.Id });

            if (sudahAda > 0)
            {
                ModelState.AddModelError(nameof(model.Nama), "Nama merek sudah digunakan.");
                return View("FormMerek", model);
            }

            var affected = await connection.ExecuteAsync(
                @"UPDATE merk
                  SET nama_merk = @Nama
                  WHERE id_merk = @Id",
                new { Nama = model.Nama.Trim(), model.Id });

            if (affected == 0)
                return NotFound();

            TempData["Sukses"] = "Merek berhasil diperbarui.";
            return RedirectToAction(nameof(Merek));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> HapusMerek(int id)
        {
            using var connection = _koneksi.GetConnection();
            await connection.OpenAsync();

            var digunakan = await connection.ExecuteScalarAsync<int>(
                @"SELECT COUNT(*)
                  FROM produk
                  WHERE id_merk = @Id",
                new { Id = id });

            if (digunakan > 0)
            {
                TempData["Gagal"] =
                    "Merek tidak dapat dihapus karena masih digunakan oleh produk.";
                return RedirectToAction(nameof(Merek));
            }

            var affected = await connection.ExecuteAsync(
                "DELETE FROM merk WHERE id_merk = @Id",
                new { Id = id });

            TempData[affected > 0 ? "Sukses" : "Gagal"] =
                affected > 0
                    ? "Merek berhasil dihapus."
                    : "Merek tidak ditemukan.";

            return RedirectToAction(nameof(Merek));
        }

        // =========================
        // HELPER
        // =========================

        private async Task<AdminMasterDataViewModel> AmbilDaftarAsync(string jenis)
        {
            using var connection = _koneksi.GetConnection();
            await connection.OpenAsync();

            if (jenis == "kategori")
            {
                var items = (await connection.QueryAsync<MasterDataItemViewModel>(
                    @"SELECT
                          c.id_category AS Id,
                          c.nama_category AS Nama,
                          COUNT(p.id_produk) AS JumlahProduk
                      FROM category c
                      LEFT JOIN produk p
                          ON c.id_category = p.id_category
                      GROUP BY c.id_category, c.nama_category
                      ORDER BY c.nama_category")).ToList();

                return new AdminMasterDataViewModel
                {
                    Jenis = "kategori",
                    Total = items.Count,
                    Items = items
                };
            }

            var merek = (await connection.QueryAsync<MasterDataItemViewModel>(
                @"SELECT
                      m.id_merk AS Id,
                      m.nama_merk AS Nama,
                      COUNT(p.id_produk) AS JumlahProduk
                  FROM merk m
                  LEFT JOIN produk p
                      ON m.id_merk = p.id_merk
                  GROUP BY m.id_merk, m.nama_merk
                  ORDER BY m.nama_merk")).ToList();

            return new AdminMasterDataViewModel
            {
                Jenis = "merek",
                Total = merek.Count,
                Items = merek
            };
        }
    }
}