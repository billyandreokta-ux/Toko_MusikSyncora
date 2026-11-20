
using System.Security.Claims;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TokoMusik.Web.Data;
using TokoMusik.Web.Models;

namespace TokoMusik.Web.Controllers
{
    [Authorize(Roles = "user")]
    public class ProfilController : Controller
    {
        private readonly Koneksi _koneksi;

        public ProfilController(Koneksi koneksi)
        {
            _koneksi = koneksi;
        }

        private int? AmbilIdUser()
        {
            var id = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (int.TryParse(id, out int idUser))
            {
                return idUser;
            }

            return null;
        }

        public async Task<IActionResult> Index()
        {
            var idUser = AmbilIdUser();

            if (idUser == null)
            {
                return RedirectToAction("Login", "Auth");
            }

            using var connection = _koneksi.GetConnection();
            await connection.OpenAsync();

            var sql = @"
                SELECT
                    c.id_customer AS IdCustomer,
                    c.nama_customer AS NamaCustomer,
                    c.email AS Email,
                    c.telepon AS Telepon,
                    u.username AS Username
                FROM customer c
                INNER JOIN users u ON c.id_user = u.id_user
                WHERE c.id_user = @IdUser
                  AND u.role = 'user'
                LIMIT 1;";

            var profil = await connection.QueryFirstOrDefaultAsync<ProfilViewModel>(
                sql,
                new { IdUser = idUser });

            if (profil == null)
            {
                return NotFound("Data profil pelanggan tidak ditemukan.");
            }

            return View(profil);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(ProfilViewModel model)
        {
            var idUser = AmbilIdUser();

            if (idUser == null)
            {
                return RedirectToAction("Login", "Auth");
            }

            if (!ModelState.IsValid)
            {
                using var readConnection = _koneksi.GetConnection();
                await readConnection.OpenAsync();

                var username = await readConnection.QueryFirstOrDefaultAsync<string>(
                    @"SELECT username
                      FROM users
                      WHERE id_user = @IdUser
                        AND role = 'user'
                      LIMIT 1;",
                    new { IdUser = idUser });

                model.Username = username ?? string.Empty;

                return View(model);
            }

            using var connection = _koneksi.GetConnection();
            await connection.OpenAsync();

            var sql = @"
                UPDATE customer c
                INNER JOIN users u ON c.id_user = u.id_user
                SET
                    c.nama_customer = @NamaCustomer,
                    c.email = @Email,
                    c.telepon = @Telepon
                WHERE c.id_user = @IdUser
                  AND u.role = 'user';";

            var affected = await connection.ExecuteAsync(
                sql,
                new
                {
                    model.NamaCustomer,
                    Email = string.IsNullOrWhiteSpace(model.Email)
                        ? null
                        : model.Email.Trim(),
                    Telepon = string.IsNullOrWhiteSpace(model.Telepon)
                        ? null
                        : model.Telepon.Trim(),
                    IdUser = idUser
                });

            if (affected > 0)
            {
                TempData["Success"] = "Profil berhasil diperbarui.";
                return RedirectToAction(nameof(Index));
            }

            TempData["Error"] = "Profil tidak berhasil diperbarui.";
            return RedirectToAction(nameof(Index));
        }
    }
}
