
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MySqlConnector;
using TokoMusik.Web.Data;
using TokoMusik.Web.Models;

namespace TokoMusik.Web.Controllers
{
    [Authorize(Roles = "admin")]
    public class AdminTarifPengirimanController : Controller
    {
        private readonly Koneksi _koneksi;

        public AdminTarifPengirimanController(Koneksi koneksi)
        {
            _koneksi = koneksi;
        }

        public async Task<IActionResult> Index()
        {
            using var connection = _koneksi.GetConnection();
            await connection.OpenAsync();

            const string sql = @"
                SELECT
                    id_tarif AS IdTarif,
                    kota AS Kota,
                    provinsi AS Provinsi,
                    biaya AS Biaya,
                    estimasi_hari AS EstimasiHari,
                    status AS Status
                FROM tarif_pengiriman
                ORDER BY
                    CASE WHEN kota = '*' THEN 0 ELSE 1 END,
                    provinsi,
                    kota;
            ";

            var data = (await connection.QueryAsync<TarifPengirimanViewModel>(
                sql)).ToList();

            return View(data);
        }

        [HttpGet]
        public IActionResult Tambah()
        {
            return View("Form", new TarifPengirimanViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Tambah(TarifPengirimanViewModel model)
        {
            if (!ModelState.IsValid)
                return View("Form", model);

            model.Kota = model.Kota.Trim();
            model.Provinsi = model.Provinsi.Trim();
            model.EstimasiHari = model.EstimasiHari?.Trim();

            if (string.IsNullOrWhiteSpace(model.EstimasiHari))
                model.EstimasiHari = null;

            try
            {
                using var connection = _koneksi.GetConnection();
                await connection.OpenAsync();

                const string sql = @"
                    INSERT INTO tarif_pengiriman
                    (kota, provinsi, biaya, estimasi_hari, status)
                    VALUES
                    (@Kota, @Provinsi, @Biaya, @EstimasiHari, 'aktif');
                ";

                await connection.ExecuteAsync(sql, model);

                TempData["Sukses"] = "Tarif pengiriman berhasil ditambahkan.";

                return RedirectToAction(nameof(Index));
            }
            catch (MySqlException ex)
            {
                ModelState.AddModelError(
                    "",
                    ex.Number == 1062
                        ? "Tarif untuk kota dan provinsi tersebut sudah tersedia."
                        : "Terjadi kesalahan database saat menyimpan tarif."
                );

                return View("Form", model);
            }
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            using var connection = _koneksi.GetConnection();
            await connection.OpenAsync();

            const string sql = @"
                SELECT
                    id_tarif AS IdTarif,
                    kota AS Kota,
                    provinsi AS Provinsi,
                    biaya AS Biaya,
                    estimasi_hari AS EstimasiHari,
                    status AS Status
                FROM tarif_pengiriman
                WHERE id_tarif = @Id;
            ";

            var model = await connection.QueryFirstOrDefaultAsync<TarifPengirimanViewModel>(
                sql, new { Id = id });

            if (model == null)
                return NotFound();

            return View("Form", model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(TarifPengirimanViewModel model)
        {
            if (!ModelState.IsValid)
                return View("Form", model);

            model.Kota = model.Kota.Trim();
            model.Provinsi = model.Provinsi.Trim();
            model.EstimasiHari = model.EstimasiHari?.Trim();

            if (string.IsNullOrWhiteSpace(model.EstimasiHari))
                model.EstimasiHari = null;

            try
            {
                using var connection = _koneksi.GetConnection();
                await connection.OpenAsync();

                const string sql = @"
                    UPDATE tarif_pengiriman
                    SET
                        kota = @Kota,
                        provinsi = @Provinsi,
                        biaya = @Biaya,
                        estimasi_hari = @EstimasiHari
                    WHERE id_tarif = @IdTarif;
                ";

                int affected = await connection.ExecuteAsync(sql, model);

                if (affected == 0)
                {
                    TempData["Gagal"] = "Tarif tidak ditemukan.";
                    return RedirectToAction(nameof(Index));
                }

                TempData["Sukses"] = "Tarif pengiriman berhasil diperbarui.";

                return RedirectToAction(nameof(Index));
            }
            catch (MySqlException ex)
            {
                ModelState.AddModelError(
                    "",
                    ex.Number == 1062
                        ? "Kombinasi kota dan provinsi sudah digunakan."
                        : "Terjadi kesalahan database saat memperbarui tarif."
                );

                return View("Form", model);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Nonaktifkan(int id)
        {
            using var connection = _koneksi.GetConnection();
            await connection.OpenAsync();

            const string sql = @"
                UPDATE tarif_pengiriman
                SET status = 'nonaktif'
                WHERE id_tarif = @Id;
            ";

            int affected = await connection.ExecuteAsync(sql, new { Id = id });

            TempData[affected > 0 ? "Sukses" : "Gagal"] =
                affected > 0
                    ? "Tarif berhasil dinonaktifkan."
                    : "Tarif tidak ditemukan.";

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Aktifkan(int id)
        {
            using var connection = _koneksi.GetConnection();
            await connection.OpenAsync();

            const string sql = @"
                UPDATE tarif_pengiriman
                SET status = 'aktif'
                WHERE id_tarif = @Id;
            ";

            int affected = await connection.ExecuteAsync(sql, new { Id = id });

            TempData[affected > 0 ? "Sukses" : "Gagal"] =
                affected > 0
                    ? "Tarif berhasil diaktifkan kembali."
                    : "Tarif tidak ditemukan.";

            return RedirectToAction(nameof(Index));
        }
    }
}
