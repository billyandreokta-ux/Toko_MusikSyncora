
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TokoMusik.Web.Data;
using TokoMusik.Web.Models;

namespace TokoMusik.Web.Controllers
{
    [Authorize(Roles = "admin")]
    [Route("AdminPromo")]
    public class AdminPromoController : Controller
    {
        private readonly Koneksi _koneksi;

        public AdminPromoController(Koneksi koneksi)
        {
            _koneksi = koneksi;
        }

        [HttpGet("")]
        public async Task<IActionResult> Index()
        {
            await using var connection = _koneksi.GetConnection();
            await connection.OpenAsync();

            const string sql = @"
                SELECT
                    id_promo AS IdPromo,
                    kode_promo AS KodePromo,
                    nama_promo AS NamaPromo,
                    tipe AS Tipe,
                    nilai AS Nilai,
                    min_belanja AS MinBelanja,
                    tgl_mulai AS TglMulai,
                    tgl_selesai AS TglSelesai,
                    status AS Status
                FROM promo
                ORDER BY id_promo DESC";

            var data = await connection.QueryAsync<PromoViewModel>(sql);

            return View(data.ToList());
        }

        [HttpGet("Tambah")]
        public IActionResult Tambah()
        {
            return View(new PromoViewModel());
        }

        [HttpPost("Tambah")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Tambah(PromoViewModel model)
        {
            ValidasiPromo(model);

            if (!ModelState.IsValid)
                return View(model);

            await using var connection = _koneksi.GetConnection();
            await connection.OpenAsync();

            const string cekSql = @"
                SELECT COUNT(*)
                FROM promo
                WHERE kode_promo = @KodePromo";

            int jumlah = await connection.ExecuteScalarAsync<int>(
                cekSql,
                new { KodePromo = model.KodePromo.Trim() });

            if (jumlah > 0)
            {
                ModelState.AddModelError(
                    nameof(model.KodePromo),
                    "Kode promo sudah digunakan.");

                return View(model);
            }

            const string sql = @"
                INSERT INTO promo
                    (kode_promo, nama_promo, tipe, nilai,
                     min_belanja, tgl_mulai, tgl_selesai, status)
                VALUES
                    (@KodePromo, @NamaPromo, @Tipe, @Nilai,
                     @MinBelanja, @TglMulai, @TglSelesai, @Status)";

            await connection.ExecuteAsync(sql, new
            {
                KodePromo = model.KodePromo.Trim().ToUpperInvariant(),
                NamaPromo = model.NamaPromo.Trim(),
                model.Tipe,
                model.Nilai,
                model.MinBelanja,
                model.TglMulai,
                model.TglSelesai,
                model.Status
            });

            TempData["Success"] = "Promo berhasil ditambahkan.";

            return RedirectToAction(nameof(Index));
        }

        [HttpGet("Edit/{id:int}")]
        public async Task<IActionResult> Edit(int id)
        {
            await using var connection = _koneksi.GetConnection();
            await connection.OpenAsync();

            const string sql = @"
                SELECT
                    id_promo AS IdPromo,
                    kode_promo AS KodePromo,
                    nama_promo AS NamaPromo,
                    tipe AS Tipe,
                    nilai AS Nilai,
                    min_belanja AS MinBelanja,
                    tgl_mulai AS TglMulai,
                    tgl_selesai AS TglSelesai,
                    status AS Status
                FROM promo
                WHERE id_promo = @Id";

            var data = await connection.QueryFirstOrDefaultAsync<PromoViewModel>(
                sql,
                new { Id = id });

            if (data == null)
                return NotFound();

            return View(data);
        }

        [HttpPost("Edit/{id:int}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, PromoViewModel model)
        {
            model.IdPromo = id;
            ValidasiPromo(model);

            if (!ModelState.IsValid)
                return View(model);

            await using var connection = _koneksi.GetConnection();
            await connection.OpenAsync();

            const string cekSql = @"
                SELECT COUNT(*)
                FROM promo
                WHERE kode_promo = @KodePromo
                  AND id_promo <> @Id";

            int jumlah = await connection.ExecuteScalarAsync<int>(
                cekSql,
                new
                {
                    KodePromo = model.KodePromo.Trim(),
                    Id = id
                });

            if (jumlah > 0)
            {
                ModelState.AddModelError(
                    nameof(model.KodePromo),
                    "Kode promo sudah digunakan oleh promo lain.");

                return View(model);
            }

            const string sql = @"
                UPDATE promo
                SET
                    kode_promo = @KodePromo,
                    nama_promo = @NamaPromo,
                    tipe = @Tipe,
                    nilai = @Nilai,
                    min_belanja = @MinBelanja,
                    tgl_mulai = @TglMulai,
                    tgl_selesai = @TglSelesai,
                    status = @Status
                WHERE id_promo = @IdPromo";

            int affected = await connection.ExecuteAsync(sql, new
            {
                IdPromo = id,
                KodePromo = model.KodePromo.Trim().ToUpperInvariant(),
                NamaPromo = model.NamaPromo.Trim(),
                model.Tipe,
                model.Nilai,
                model.MinBelanja,
                model.TglMulai,
                model.TglSelesai,
                model.Status
            });

            if (affected == 0)
                return NotFound();

            TempData["Success"] = "Promo berhasil diperbarui.";

            return RedirectToAction(nameof(Index));
        }

        [HttpPost("Nonaktifkan/{id:int}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Nonaktifkan(int id)
        {
            await using var connection = _koneksi.GetConnection();
            await connection.OpenAsync();

            const string sql = @"
                UPDATE promo
                SET status = 'nonaktif'
                WHERE id_promo = @Id";

            int affected = await connection.ExecuteAsync(
                sql,
                new { Id = id });

            TempData[affected > 0 ? "Success" : "Error"] =
                affected > 0
                    ? "Promo berhasil dinonaktifkan."
                    : "Promo tidak ditemukan.";

            return RedirectToAction(nameof(Index));
        }

        private void ValidasiPromo(PromoViewModel model)
        {
            model.KodePromo = model.KodePromo?.Trim() ?? "";
            model.NamaPromo = model.NamaPromo?.Trim() ?? "";

            if (model.Tipe != "persen" && model.Tipe != "nominal")
            {
                ModelState.AddModelError(
                    nameof(model.Tipe),
                    "Tipe promo tidak valid.");
            }

            if (model.Tipe == "persen" && model.Nilai > 100)
            {
                ModelState.AddModelError(
                    nameof(model.Nilai),
                    "Promo persentase tidak boleh lebih dari 100%.");
            }

            if (model.TglSelesai.Date < model.TglMulai.Date)
            {
                ModelState.AddModelError(
                    nameof(model.TglSelesai),
                    "Tanggal selesai tidak boleh sebelum tanggal mulai.");
            }

            if (model.Status != "aktif" && model.Status != "nonaktif")
            {
                ModelState.AddModelError(
                    nameof(model.Status),
                    "Status promo tidak valid.");
            }
        }
    }
}
