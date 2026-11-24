
using System.Security.Claims;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TokoMusik.Web.Data;
using TokoMusik.Web.Models;

namespace TokoMusik.Web.Controllers
{
    [Authorize]
    public class GaransiController : Controller
    {
        private readonly Koneksi _koneksi;

        public GaransiController(Koneksi koneksi)
        {
            _koneksi = koneksi;
        }

        private int IdUser =>
            int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int id)
                ? id : 0;

        private async Task<int> AmbilIdCustomer()
        {
            using var connection = _koneksi.GetConnection();

            return await connection.ExecuteScalarAsync<int?>(
                "SELECT id_customer FROM customer WHERE id_user = @IdUser",
                new { IdUser }) ?? 0;
        }

        public async Task<IActionResult> Index()
        {
            int idCustomer = await AmbilIdCustomer();

            if (idCustomer == 0)
                return Forbid();

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
                    END AS Status
                FROM garansi g
                INNER JOIN detail_pesanan dp
                    ON dp.id_detail_pesanan = g.id_detail_pesanan
                INNER JOIN pemesanan pm
                    ON pm.id_pemesanan = dp.id_pemesanan
                INNER JOIN produk p
                    ON p.id_produk = dp.id_produk
                WHERE pm.id_customer = @IdCustomer
                ORDER BY g.tgl_mulai DESC",
                new { IdCustomer = idCustomer });

            return View(new GaransiViewModel
            {
                DaftarGaransi = data.ToList()
            });
        }

        [HttpGet]
        public async Task<IActionResult> Klaim(int id)
        {
            int idCustomer = await AmbilIdCustomer();

            if (idCustomer == 0)
                return Forbid();

            using var connection = _koneksi.GetConnection();

            var garansi = await connection.QueryFirstOrDefaultAsync<KlaimGaransiViewModel>(
                @"
                SELECT
                    g.id_garansi AS IdGaransi,
                    p.nama_produk AS NamaProduk,
                    p.kode_produk AS KodeProduk,
                    g.tgl_berakhir AS TanggalBerakhir
                FROM garansi g
                INNER JOIN detail_pesanan dp
                    ON dp.id_detail_pesanan = g.id_detail_pesanan
                INNER JOIN pemesanan pm
                    ON pm.id_pemesanan = dp.id_pemesanan
                INNER JOIN produk p
                    ON p.id_produk = dp.id_produk
                WHERE g.id_garansi = @Id
                  AND pm.id_customer = @IdCustomer
                  AND g.status = 'aktif'
                  AND g.tgl_berakhir >= CURDATE()",
                new { Id = id, IdCustomer = idCustomer });

            if (garansi == null)
                return NotFound();

            return View(garansi);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Klaim(KlaimGaransiViewModel model)
        {
            int idCustomer = await AmbilIdCustomer();

            if (idCustomer == 0)
                return Forbid();

            using var connection = _koneksi.GetConnection();

            if (!ModelState.IsValid)
            {
                var informasi = await AmbilInformasiKlaim(
                    connection, model.IdGaransi, idCustomer);

                if (informasi == null)
                    return NotFound();

                model.NamaProduk = informasi.NamaProduk;
                model.KodeProduk = informasi.KodeProduk;
                model.TanggalBerakhir = informasi.TanggalBerakhir;

                return View(model);
            }

            int affected = await connection.ExecuteAsync(
                @"
                UPDATE garansi g
                INNER JOIN detail_pesanan dp
                    ON dp.id_detail_pesanan = g.id_detail_pesanan
                INNER JOIN pemesanan pm
                    ON pm.id_pemesanan = dp.id_pemesanan
                SET
                    g.status = 'diklaim',
                    g.keterangan = LEFT(
                        CONCAT(
                            COALESCE(g.keterangan, ''),
                            CASE
                                WHEN COALESCE(g.keterangan, '') = ''
                                THEN ''
                                ELSE ' | '
                            END,
                            'Klaim pelanggan: ',
                            @Keterangan
                        ),
                        255
                    )
                WHERE g.id_garansi = @IdGaransi
                  AND pm.id_customer = @IdCustomer
                  AND g.status = 'aktif'
                  AND g.tgl_berakhir >= CURDATE()",
                new
                {
                    model.IdGaransi,
                    IdCustomer = idCustomer,
                    Keterangan = model.KeteranganKlaim.Trim()
                });

            if (affected == 0)
            {
                TempData["Error"] =
                    "Klaim tidak dapat diproses. Garansi mungkin sudah berakhir atau pernah diklaim.";

                return RedirectToAction(nameof(Index));
            }

            TempData["Success"] =
                "Klaim garansi berhasil dicatat. Silakan hubungi admin untuk tindak lanjut.";

            return RedirectToAction(nameof(Index));
        }

        private async Task<KlaimGaransiViewModel?> AmbilInformasiKlaim(
            System.Data.IDbConnection connection,
            int idGaransi,
            int idCustomer)
        {
            return await connection.QueryFirstOrDefaultAsync<KlaimGaransiViewModel>(
                @"
                SELECT
                    g.id_garansi AS IdGaransi,
                    p.nama_produk AS NamaProduk,
                    p.kode_produk AS KodeProduk,
                    g.tgl_berakhir AS TanggalBerakhir
                FROM garansi g
                INNER JOIN detail_pesanan dp
                    ON dp.id_detail_pesanan = g.id_detail_pesanan
                INNER JOIN pemesanan pm
                    ON pm.id_pemesanan = dp.id_pemesanan
                INNER JOIN produk p
                    ON p.id_produk = dp.id_produk
                WHERE g.id_garansi = @IdGaransi
                  AND pm.id_customer = @IdCustomer
                  AND g.status = 'aktif'
                  AND g.tgl_berakhir >= CURDATE()",
                new
                {
                    IdGaransi = idGaransi,
                    IdCustomer = idCustomer
                });
        }
    }
}
