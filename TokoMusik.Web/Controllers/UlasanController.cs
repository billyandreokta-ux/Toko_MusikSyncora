
using System.Security.Claims;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TokoMusik.Web.Data;
using TokoMusik.Web.Models;

namespace TokoMusik.Web.Controllers
{
    [Authorize(Roles = "user")]
    [Route("Ulasan")]
    public class UlasanController : Controller
    {
        private readonly Koneksi _koneksi;

        public UlasanController(Koneksi koneksi)
        {
            _koneksi = koneksi;
        }

        private async Task<int?> GetIdCustomerAsync(
            System.Data.IDbConnection connection)
        {
            string? idUser = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (!int.TryParse(idUser, out int idUserInt))
                return null;

            return await connection.QueryFirstOrDefaultAsync<int?>(
                @"SELECT id_customer
                  FROM customer
                  WHERE id_user = @IdUser",
                new { IdUser = idUserInt });
        }

        [HttpGet("")]
        public async Task<IActionResult> Index()
        {
            await using var connection = _koneksi.GetConnection();
            await connection.OpenAsync();

            int? idCustomer = await GetIdCustomerAsync(connection);

            if (idCustomer == null)
                return Forbid();

            const string sqlProduk = @"
                SELECT DISTINCT
                    pr.id_produk AS IdProduk,
                    pr.nama_produk AS NamaProduk,
                    pr.kode_produk AS KodeProduk,
                    pr.foto AS Foto
                FROM detail_pesanan dp
                INNER JOIN pemesanan p
                    ON p.id_pemesanan = dp.id_pemesanan
                INNER JOIN produk pr
                    ON pr.id_produk = dp.id_produk
                WHERE p.id_customer = @IdCustomer
                  AND p.status = 'selesai'
                  AND NOT EXISTS (
                      SELECT 1
                      FROM ulasan u
                      WHERE u.id_produk = pr.id_produk
                        AND u.id_customer = @IdCustomer
                  )
                ORDER BY pr.nama_produk";

            const string sqlRiwayat = @"
                SELECT
                    u.id_ulasan AS IdUlasan,
                    pr.nama_produk AS NamaProduk,
                    u.rating AS Rating,
                    u.komentar AS Komentar,
                    u.tgl_ulasan AS TglUlasan
                FROM ulasan u
                INNER JOIN produk pr
                    ON pr.id_produk = u.id_produk
                WHERE u.id_customer = @IdCustomer
                ORDER BY u.tgl_ulasan DESC";

            var model = new UlasanHalamanViewModel
            {
                ProdukBelumDiulas =
                    (await connection.QueryAsync<UlasanViewModel>(
                        sqlProduk,
                        new { IdCustomer = idCustomer.Value })).ToList(),

                RiwayatUlasan =
                    (await connection.QueryAsync<UlasanRiwayatViewModel>(
                        sqlRiwayat,
                        new { IdCustomer = idCustomer.Value })).ToList()
            };

            return View(model);
        }

        [HttpPost("Kirim")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Kirim(UlasanViewModel model)
        {
            if (model.Rating < 1 || model.Rating > 5)
            {
                TempData["Error"] = "Rating harus antara 1 sampai 5.";
                return RedirectToAction(nameof(Index));
            }

            if (model.IdProduk <= 0)
            {
                TempData["Error"] = "Produk tidak valid.";
                return RedirectToAction(nameof(Index));
            }

            if (!ModelState.IsValid)
            {
                TempData["Error"] = "Komentar terlalu panjang atau data tidak valid.";
                return RedirectToAction(nameof(Index));
            }

            string? idUser = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (!int.TryParse(idUser, out int idUserInt))
                return Forbid();

            await using var connection = _koneksi.GetConnection();
            await connection.OpenAsync();

            const string sql = @"
                INSERT INTO ulasan
                    (id_produk, id_customer, rating, komentar, tgl_ulasan)
                SELECT
                    @IdProduk,
                    c.id_customer,
                    @Rating,
                    @Komentar,
                    CURRENT_TIMESTAMP
                FROM customer c
                WHERE c.id_user = @IdUser
                  AND EXISTS (
                      SELECT 1
                      FROM detail_pesanan dp
                      INNER JOIN pemesanan p
                          ON p.id_pemesanan = dp.id_pemesanan
                      WHERE dp.id_produk = @IdProduk
                        AND p.id_customer = c.id_customer
                        AND p.status = 'selesai'
                  )
                  AND NOT EXISTS (
                      SELECT 1
                      FROM ulasan u
                      WHERE u.id_produk = @IdProduk
                        AND u.id_customer = c.id_customer
                  )";

            int affected = await connection.ExecuteAsync(sql, new
            {
                model.IdProduk,
                model.Rating,
                Komentar = string.IsNullOrWhiteSpace(model.Komentar)
                    ? null
                    : model.Komentar.Trim(),
                IdUser = idUserInt
            });

            if (affected == 0)
            {
                TempData["Error"] =
                    "Ulasan tidak dapat dikirim. Pastikan pesanan sudah selesai dan produk belum pernah diulas.";

                return RedirectToAction(nameof(Index));
            }

            TempData["Success"] = "Terima kasih! Ulasan berhasil dikirim.";

            return RedirectToAction(nameof(Index));
        }
    }
}
