
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TokoMusik.Web.Data;

namespace TokoMusik.Web.Controllers
{
    [Authorize(Roles = "admin")]
    public class AdminController : Controller
    {
        private readonly Koneksi _koneksi;

        public AdminController(Koneksi koneksi)
        {
            _koneksi = koneksi;
        }

        public async Task<IActionResult> Index()
        {
            using var connection = _koneksi.GetConnection();
            await connection.OpenAsync();

            ViewBag.TotalProduk = await connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM produk WHERE status = 'aktif'"
            );

            ViewBag.TotalCustomer = await connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM customer"
            );

            ViewBag.TotalPesanan = await connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM pemesanan"
            );

            ViewBag.TotalPendapatan =
                await connection.ExecuteScalarAsync<decimal>(@"
                    SELECT COALESCE(SUM(total_bayar), 0)
                    FROM pemesanan
                    WHERE status IN (
                        'dibayar',
                        'diproses',
                        'dikirim',
                        'selesai'
                    )
                ");

            ViewBag.PesananTerbaru =
                await connection.QueryAsync(@"
                    SELECT
                        p.id_pemesanan AS IdPesanan,
                        c.nama_customer AS NamaCustomer,
                        p.tgl_pesan AS TanggalPesan,
                        p.total_bayar AS TotalBayar,
                        p.status AS Status
                    FROM pemesanan p
                    INNER JOIN customer c
                        ON p.id_customer = c.id_customer
                    ORDER BY p.tgl_pesan DESC
                    LIMIT 8
                ");

            return View();
        }
    }
}
