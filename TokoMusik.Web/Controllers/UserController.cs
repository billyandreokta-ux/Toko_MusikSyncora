
using Dapper;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TokoMusik.Web.Data;

namespace TokoMusik.Web.Controllers
{
    [Authorize(Roles = "user")]
    public class UserController : Controller
    {
        private readonly Koneksi _koneksi;

        public UserController(Koneksi koneksi)
        {
            _koneksi = koneksi;
        }

        public async Task<IActionResult> Index()
        {
            string? idUser = User.FindFirstValue(
                ClaimTypes.NameIdentifier
            );

            using var connection = _koneksi.GetConnection();
            await connection.OpenAsync();

            const string customerSql = @"
                SELECT
                    id_customer AS IdCustomer,
                    nama_customer AS NamaCustomer,
                    email AS Email,
                    telepon AS Telepon
                FROM customer
                WHERE id_user = @IdUser
                LIMIT 1;
            ";

            var customer = await connection.QueryFirstOrDefaultAsync(
                customerSql,
                new { IdUser = idUser }
            );

            if (customer == null)
            {
                await HttpContext.SignOutAsync("TokoMusikCookie");
                return RedirectToAction("Login", "Auth");
            }

            ViewBag.Customer = customer;

            ViewBag.Pesanan = await connection.QueryAsync(@"
                SELECT
                    id_pemesanan AS IdPesanan,
                    tgl_pesan AS TanggalPesan,
                    total_bayar AS TotalBayar,
                    status AS Status
                FROM pemesanan
                WHERE id_customer = @IdCustomer
                ORDER BY tgl_pesan DESC
                LIMIT 10;
            ", new { IdCustomer = (int)customer.IdCustomer });

            ViewBag.TotalPesanan = await connection.ExecuteScalarAsync<int>(
                @"
                    SELECT COUNT(*)
                    FROM pemesanan
                    WHERE id_customer = @IdCustomer
                ",
                new { IdCustomer = (int)customer.IdCustomer }
            );

            return View();
        }
    }
}
