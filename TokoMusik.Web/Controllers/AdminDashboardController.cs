
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TokoMusik.Web.Data;
using TokoMusik.Web.Models;

namespace TokoMusik.Web.Controllers
{
    [Authorize(Roles = "admin")]
    public class AdminDashboardController : Controller
    {
        private readonly Koneksi _koneksi;

        public AdminDashboardController(Koneksi koneksi)
        {
            _koneksi = koneksi;
        }

        public async Task<IActionResult> Index()
        {
            var model = new AdminDashboardViewModel();

            using var connection = _koneksi.GetConnection();
            await connection.OpenAsync();

            model.TotalProduk = await connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM produk");

            model.TotalPelanggan = await connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM customer");

            model.TotalPesanan = await connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM pemesanan");

            model.PesananHariIni = await connection.ExecuteScalarAsync<int>(
                @"SELECT COUNT(*)
                  FROM pemesanan
                  WHERE DATE(tgl_pesan) = CURDATE()");

            model.PembayaranMenunggu = await connection.ExecuteScalarAsync<int>(
                @"SELECT COUNT(*)
                  FROM payments
                  WHERE status = 'pending'");

            model.TotalPendapatan = await connection.ExecuteScalarAsync<decimal>(
                @"SELECT COALESCE(SUM(jumlah_bayar), 0)
                  FROM payments
                  WHERE status = 'berhasil'");

            model.ProdukStokMenipis = await connection.ExecuteScalarAsync<int>(
                @"SELECT COUNT(*)
                  FROM stock
                  WHERE jumlah <= stok_minimum");

            model.PesananTerbaru = (await connection.QueryAsync<PesananTerbaruViewModel>(
                @"SELECT
                      p.id_pemesanan AS IdPemesanan,
                      c.nama_customer AS NamaCustomer,
                      p.tgl_pesan AS TglPesan,
                      p.total_bayar AS TotalBayar,
                      p.status AS Status
                  FROM pemesanan p
                  INNER JOIN customer c
                      ON p.id_customer = c.id_customer
                  ORDER BY p.tgl_pesan DESC
                  LIMIT 8")).ToList();

            model.StokMenipis = (await connection.QueryAsync<StokMenipisViewModel>(
                @"SELECT
                      pr.id_produk AS IdProduk,
                      pr.nama_produk AS NamaProduk,
                      s.jumlah AS Jumlah,
                      s.stok_minimum AS StokMinimum
                  FROM stock s
                  INNER JOIN produk pr
                      ON s.id_produk = pr.id_produk
                  WHERE s.jumlah <= s.stok_minimum
                  ORDER BY s.jumlah ASC
                  LIMIT 8")).ToList();

            return View(model);
        }
    }
}