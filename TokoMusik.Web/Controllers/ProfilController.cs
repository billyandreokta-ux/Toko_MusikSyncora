
using System.Security.Claims;
using Dapper;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using MySqlConnector;
using TokoMusik.Web.Data;
using TokoMusik.Web.Models;

namespace TokoMusik.Web.Controllers
{
    [Authorize(Roles = "user")]
    public class ProfilController : Controller
    {
        private readonly Koneksi _koneksi;
        private readonly IWebHostEnvironment _environment;

        private const string CookieScheme = "TokoMusikCookie";
        private const long MaksimalUkuranFoto = 5 * 1024 * 1024;

        private static readonly string[] EkstensiFotoDiizinkan =
        {
            ".jpg", ".jpeg", ".png", ".webp"
        };

        public ProfilController(
            Koneksi koneksi,
            IWebHostEnvironment environment)
        {
            _koneksi = koneksi;
            _environment = environment;
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

        private string FolderFoto
        {
            get
            {
                var webRoot = _environment.WebRootPath
                    ?? Path.Combine(_environment.ContentRootPath, "wwwroot");

                return Path.Combine(webRoot, "uploads", "profil");
            }
        }

        private async Task<ProfilViewModel?> AmbilProfilAsync(int idUser)
        {
            using var connection = _koneksi.GetConnection();
            await connection.OpenAsync();

            const string sql = @"
                SELECT
                    c.id_customer AS IdCustomer,
                    c.nama_customer AS NamaCustomer,
                    c.email AS Email,
                    c.telepon AS Telepon,
                    c.foto_profil AS Foto,
                    u.username AS Username
                FROM customer c
                INNER JOIN users u
                    ON u.id_user = c.id_user
                WHERE c.id_user = @IdUser
                  AND u.role = 'user'
                LIMIT 1;";

            return await connection.QueryFirstOrDefaultAsync<ProfilViewModel>(
                sql,
                new { IdUser = idUser });
        }

        private async Task PerbaruiCookieProfilAsync(
            string username,
            string? foto)
        {
            var claims = User.Claims
                .Where(c =>
                    c.Type != ClaimTypes.Name &&
                    c.Type != "FotoProfil")
                .ToList();

            claims.Add(new Claim(ClaimTypes.Name, username));

            if (!string.IsNullOrWhiteSpace(foto))
            {
                claims.Add(new Claim("FotoProfil", foto));
            }

            var identity = new ClaimsIdentity(
                claims,
                CookieScheme);

            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync(
                CookieScheme,
                principal);
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var idUser = AmbilIdUser();

            if (idUser == null)
            {
                return RedirectToAction("Login", "Auth");
            }

            var profil = await AmbilProfilAsync(idUser.Value);

            if (profil == null)
            {
                return NotFound("Data profil pelanggan tidak ditemukan.");
            }

            return View(profil);
        }

        [HttpGet]
        public async Task<IActionResult> Edit()
        {
            var idUser = AmbilIdUser();

            if (idUser == null)
            {
                return RedirectToAction("Login", "Auth");
            }

            var profil = await AmbilProfilAsync(idUser.Value);

            if (profil == null)
            {
                return NotFound("Data profil pelanggan tidak ditemukan.");
            }

            return View(profil);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(ProfilViewModel model)
        {
            var idUser = AmbilIdUser();

            if (idUser == null)
            {
                return RedirectToAction("Login", "Auth");
            }

            var profilLama = await AmbilProfilAsync(idUser.Value);

            if (profilLama == null)
            {
                return NotFound("Data profil pelanggan tidak ditemukan.");
            }

            model.IdCustomer = profilLama.IdCustomer;
            model.Foto = profilLama.Foto;

            if (model.FotoFile != null && model.FotoFile.Length > 0)
            {
                if (model.FotoFile.Length > MaksimalUkuranFoto)
                {
                    ModelState.AddModelError(
                        nameof(model.FotoFile),
                        "Ukuran foto maksimal 5 MB.");
                }
                else
                {
                    var ekstensi = Path.GetExtension(
                        model.FotoFile.FileName).ToLowerInvariant();

                    if (!EkstensiFotoDiizinkan.Contains(ekstensi))
                    {
                        ModelState.AddModelError(
                            nameof(model.FotoFile),
                            "Format foto harus JPG, JPEG, PNG, atau WEBP.");
                    }
                }
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var username = model.Username.Trim();

            using var connection = _koneksi.GetConnection();
            await connection.OpenAsync();

            const string sqlCekUsername = @"
                SELECT COUNT(*)
                FROM users
                WHERE username = @Username
                  AND id_user <> @IdUser;";

            var usernameDipakai =
                await connection.ExecuteScalarAsync<int>(
                    sqlCekUsername,
                    new
                    {
                        Username = username,
                        IdUser = idUser
                    });

            if (usernameDipakai > 0)
            {
                ModelState.AddModelError(
                    nameof(model.Username),
                    "Username tersebut sudah digunakan akun lain.");

                return View(model);
            }

            string? fotoBaru = profilLama.Foto;
            string? pathFotoBaru = null;

            if (model.FotoFile != null && model.FotoFile.Length > 0)
            {
                var ekstensi = Path.GetExtension(
                    model.FotoFile.FileName).ToLowerInvariant();

                fotoBaru = $"{Guid.NewGuid():N}{ekstensi}";

                Directory.CreateDirectory(FolderFoto);

                pathFotoBaru = Path.Combine(FolderFoto, fotoBaru);

                await using var stream = new FileStream(
                    pathFotoBaru,
                    FileMode.CreateNew);

                await model.FotoFile.CopyToAsync(stream);
            }

            try
            {
                await using var transaction =
                    await connection.BeginTransactionAsync();

                const string sqlUpdate = @"
                    UPDATE customer c
                    INNER JOIN users u
                        ON u.id_user = c.id_user
                    SET
                        c.nama_customer = @NamaCustomer,
                        c.email = @Email,
                        c.telepon = @Telepon,
                        c.foto_profil = @Foto,
                        u.username = @Username
                    WHERE c.id_user = @IdUser
                      AND u.role = 'user';";

                var affected = await connection.ExecuteAsync(
                    sqlUpdate,
                    new
                    {
                        NamaCustomer = model.NamaCustomer.Trim(),

                        Email = string.IsNullOrWhiteSpace(model.Email)
                            ? null
                            : model.Email.Trim(),

                        Telepon = string.IsNullOrWhiteSpace(model.Telepon)
                            ? null
                            : model.Telepon.Trim(),

                        Foto = fotoBaru,
                        Username = username,
                        IdUser = idUser
                    },
                    transaction);

                if (affected == 0)
                {
                    await transaction.RollbackAsync();

                    if (pathFotoBaru != null &&
                        System.IO.File.Exists(pathFotoBaru))
                    {
                        System.IO.File.Delete(pathFotoBaru);
                    }

                    TempData["Error"] = "Profil tidak berhasil diperbarui.";
                    return RedirectToAction(nameof(Index));
                }

                await transaction.CommitAsync();

                await PerbaruiCookieProfilAsync(username, fotoBaru);

                if (!string.IsNullOrWhiteSpace(pathFotoBaru) &&
                    !string.IsNullOrWhiteSpace(profilLama.Foto))
                {
                    HapusFotoLama(profilLama.Foto);
                }

                TempData["Success"] = "Profil berhasil diperbarui.";
                return RedirectToAction(nameof(Index));
            }
            catch (MySqlException ex) when (ex.Number == 1062)
            {
                if (pathFotoBaru != null &&
                    System.IO.File.Exists(pathFotoBaru))
                {
                    System.IO.File.Delete(pathFotoBaru);
                }

                ModelState.AddModelError(
                    nameof(model.Username),
                    "Username tersebut sudah digunakan.");

                return View(model);
            }
            catch
            {
                if (pathFotoBaru != null &&
                    System.IO.File.Exists(pathFotoBaru))
                {
                    System.IO.File.Delete(pathFotoBaru);
                }

                TempData["Error"] =
                    "Terjadi kesalahan saat menyimpan profil. Silakan coba kembali.";

                return RedirectToAction(nameof(Edit));
            }
        }

        private void HapusFotoLama(string namaFile)
        {
            var namaAman = Path.GetFileName(namaFile);
            var path = Path.Combine(FolderFoto, namaAman);

            if (System.IO.File.Exists(path))
            {
                System.IO.File.Delete(path);
            }
        }
    }
}
