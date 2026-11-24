
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TokoMusik.Web.Data;
using TokoMusik.Web.Models;

namespace TokoMusik.Web.Controllers
{
    [Authorize(Roles = "admin")]
    public class AdminGaransiController : Controller
    {
        private readonly Koneksi _koneksi;

        public AdminGaransiController(Koneksi koneksi)
        {
            _koneksi = koneksi;
        }

        public async Task<IActionResult> Index(string? status, string? cari)
        {
            using var connection = _koneksi.GetConnection();

            var data = await connection.QueryAsync<GaransiItemViewModel>(
                @"
                SELECT
                    g.id_garansi AS IdGaransi,
                    g.id_detail_pesanan AS IdDetailPesanan,
                    dp.id_pemesanan AS IdPemesanan,
                    p.kode_produk AS KodeProduk,
                    p.nama_produk AS NamaProduk,
                    dp.jumlah AS Jumlah,
                    g.tgl_mulai AS TanggalMulai,
                    g.tgl_berakhir AS TanggalBerakhir,
                    g.keterangan AS Keterangan,
                    CASE
                        WHEN g.status = 'aktif'
                             AND g.tgl_berakhir < CURDATE()
                        THEN 'habis'
                        ELSE g.status
                    END AS Status,
                    c.nama_customer AS NamaCustomer
                FROM garansi g
                INNER JOIN detail_pesanan dp
                    ON dp.id_detail_pesanan = g.id_detail_pesanan
                INNER JOIN pemesanan pm
                    ON pm.id_pemesanan = dp.id_pemesanan
                INNER JOIN produk p
                    ON p.id_produk = dp.id_produk
                INNER JOIN customer c
                    ON c.id_customer = pm.id_customer
                WHERE (
                    @Status IS NULL
                    OR (
                        @Status = 'habis'
                        AND (
                            g.status = 'habis'
                            OR (g.status = 'aktif' AND g.tgl_berakhir < CURDATE())
                        )
                    )
                    OR (
                        @Status <> 'habis'
                        AND g.status = @Status
                        AND g.tgl_berakhir >= CURDATE()
                    )
                )
                AND (
                    @Cari IS NULL
                    OR p.nama_produk LIKE CONCAT('%', @Cari, '%')
                    OR p.kode_produk LIKE CONCAT('%', @Cari, '%')
                    OR c.nama_customer LIKE CONCAT('%', @Cari, '%')
                    OR CAST(pm.id_pemesanan AS CHAR) LIKE CONCAT('%', @Cari, '%')
                )
                ORDER BY g.tgl_mulai DESC",
                new
                {
                    Status = string.IsNullOrWhiteSpace(status) ||
                             status == "semua" ? null : status,
                    Cari = string.IsNullOrWhiteSpace(cari) ? null : cari.Trim()
                });

            ViewBag.StatusFilter = status ?? "semua";
            ViewBag.KataKunci = cari ?? "";

            return View(data.ToList());
        }

        [HttpGet]
        public async Task<IActionResult> Tambah()
        {
            var model = new TambahGaransiViewModel();
            await MuatDetailPesanan(model);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Tambah(TambahGaransiViewModel model)
        {
            if (model.TanggalBerakhir < model.TanggalMulai)
            {
                ModelState.AddModelError(
                    nameof(model.TanggalBerakhir),
                    "Tanggal berakhir tidak boleh sebelum tanggal mulai.");
            }

            if (!ModelState.IsValid)
            {
                await MuatDetailPesanan(model);
                return View(model);
            }

            using var connection = _koneksi.GetConnection();

            var valid = await connection.ExecuteScalarAsync<int>(
                @"
                SELECT COUNT(*)
                FROM detail_pesanan dp
                INNER JOIN pemesanan pm
                    ON pm.id_pemesanan = dp.id_pemesanan
                WHERE dp.id_detail_pesanan = @Id
                  AND pm.status = 'selesai'
                  AND NOT EXISTS (
                      SELECT 1
                      FROM garansi g
                      WHERE g.id_detail_pesanan = dp.id_detail_pesanan
                  )",
                new { Id = model.IdDetailPesanan });

            if (valid == 0)
            {
                ModelState.AddModelError(
                    nameof(model.IdDetailPesanan),
                    "Detail pesanan tidak valid, belum selesai, atau sudah memiliki garansi.");

                await MuatDetailPesanan(model);
                return View(model);
            }

            await connection.ExecuteAsync(
                @"
                INSERT INTO garansi
                    (id_detail_pesanan, tgl_mulai, tgl_berakhir, keterangan, status)
                VALUES
                    (@IdDetailPesanan, @TanggalMulai, @TanggalBerakhir,
                     @Keterangan, 'aktif')",
                new
                {
                    model.IdDetailPesanan,
                    model.TanggalMulai,
                    model.TanggalBerakhir,
                    Keterangan = string.IsNullOrWhiteSpace(model.Keterangan)
                        ? null : model.Keterangan.Trim()
                });

            TempData["Success"] = "Garansi produk berhasil ditambahkan.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UbahStatus(int idGaransi, string status)
        {
            string[] statusValid = { "aktif", "habis", "diklaim" };

            if (!statusValid.Contains(status))
            {
                TempData["Error"] = "Status garansi tidak valid.";
                return RedirectToAction(nameof(Index));
            }

            using var connection = _koneksi.GetConnection();

            int affected = await connection.ExecuteAsync(
                @"
                UPDATE garansi
                SET status = @Status
                WHERE id_garansi = @IdGaransi",
                new { IdGaransi = idGaransi, Status = status });

            TempData[affected > 0 ? "Success" : "Error"] =
                affected > 0
                    ? "Status garansi berhasil diperbarui."
                    : "Data garansi tidak ditemukan.";

            return RedirectToAction(nameof(Index));
        }

        private async Task MuatDetailPesanan(TambahGaransiViewModel model)
        {
            using var connection = _koneksi.GetConnection();

            var data = await connection.QueryAsync<PilihanDetailGaransiViewModel>(
                @"
                SELECT
                    dp.id_detail_pesanan AS IdDetailPesanan,
                    p.nama_produk AS NamaProduk,
                    p.kode_produk AS KodeProduk,
                    pm.id_pemesanan AS IdPemesanan,
                    c.nama_customer AS NamaCustomer
                FROM detail_pesanan dp
                INNER JOIN pemesanan pm
                    ON pm.id_pemesanan = dp.id_pemesanan
                INNER JOIN produk p
                    ON p.id_produk = dp.id_produk
                INNER JOIN customer c
                    ON c.id_customer = pm.id_customer
                WHERE pm.status = 'selesai'
                  AND NOT EXISTS (
                      SELECT 1
                      FROM garansi g
                      WHERE g.id_detail_pesanan = dp.id_detail_pesanan
                  )
                ORDER BY pm.tgl_pesan DESC");

            model.DaftarDetailPesanan = data.ToList();
        }
    }
}
