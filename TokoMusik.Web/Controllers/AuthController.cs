
using System.Security.Claims;
using Dapper;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MySqlConnector;
using TokoMusik.Web.Data;
using TokoMusik.Web.DTOs;

namespace TokoMusik.Web.Controllers
{
    public class AuthController : Controller
    {
        private readonly Koneksi _koneksi;

        public AuthController(Koneksi koneksi)
        {
            _koneksi = koneksi;
        }

        // =========================================
        // LOGIN - HALAMAN
        // =========================================
        [AllowAnonymous]
        [HttpGet]
        public IActionResult Login()
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectToAction("Index", "Home");
            }

            return View();
        }

        // =========================================
        // LOGIN - PROSES
        // =========================================
        [AllowAnonymous]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginRequest request)
        {
            if (!ModelState.IsValid)
            {
                return View(request);
            }

            using var connection = _koneksi.GetConnection();
            await connection.OpenAsync();

            const string sql = @"
                SELECT
                    u.id_user AS IdUser,
                    u.username AS Username,
                    u.password_hash AS PasswordHash,
                    u.role AS Role,
                    u.status AS Status,
                    c.id_customer AS IdCustomer
                FROM users u
                LEFT JOIN customer c ON c.id_user = u.id_user
                WHERE u.username = @Username
                LIMIT 1;
            ";

            var account = await connection.QuerySingleOrDefaultAsync<LoginData>(
                sql,
                new { request.Username }
            );

            if (account == null ||
                account.Status != "aktif" ||
                !global::BCrypt.Net.BCrypt.Verify(
                    request.Password,
                    account.PasswordHash
                ))
            {
                ModelState.AddModelError(
                    string.Empty,
                    "Username atau password tidak valid."
                );

                return View(request);
            }

            var claims = new List<Claim>
            {
                new Claim(
                    ClaimTypes.NameIdentifier,
                    account.IdUser.ToString()
                ),
                new Claim(
                    ClaimTypes.Name,
                    account.Username
                ),
                new Claim(
                    ClaimTypes.Role,
                    account.Role
                )
            };

            if (account.IdCustomer.HasValue)
            {
                claims.Add(new Claim(
                    "IdCustomer",
                    account.IdCustomer.Value.ToString()
                ));
            }

            var identity = new ClaimsIdentity(
                claims,
                "TokoMusikCookie"
            );

            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync(
                "TokoMusikCookie",
                principal
            );

            if (account.Role == "admin")
            {
                return RedirectToAction("Index", "Admin");
            }

            return RedirectToAction("Index", "User");
        }

        // =========================================
        // REGISTER - HALAMAN
        // =========================================
        [AllowAnonymous]
        [HttpGet]
        public IActionResult Register()
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectToAction("Index", "Home");
            }

            return View();
        }

        // =========================================
        // REGISTER - PROSES
        // =========================================
        [AllowAnonymous]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterRequest request)
        {
            if (!ModelState.IsValid)
            {
                return View(request);
            }

            using var connection = _koneksi.GetConnection();
            await connection.OpenAsync();

            using var transaction = await connection.BeginTransactionAsync();

            try
            {
                const string cekUsername = @"
                    SELECT COUNT(*)
                    FROM users
                    WHERE username = @Username;
                ";

                int jumlahUsername = await connection.ExecuteScalarAsync<int>(
                    cekUsername,
                    new { request.Username },
                    transaction
                );

                if (jumlahUsername > 0)
                {
                    ModelState.AddModelError(
                        "Username",
                        "Username sudah digunakan."
                    );

                    await transaction.RollbackAsync();

                    return View(request);
                }

                string passwordHash =
                    global::BCrypt.Net.BCrypt.HashPassword(
                        request.Password
                    );

                const string insertUser = @"
                    INSERT INTO users
                    (username, password_hash, role, status)
                    VALUES
                    (@Username, @PasswordHash, 'user', 'aktif');

                    SELECT LAST_INSERT_ID();
                ";

                int idUser = await connection.ExecuteScalarAsync<int>(
                    insertUser,
                    new
                    {
                        request.Username,
                        PasswordHash = passwordHash
                    },
                    transaction
                );

                const string insertCustomer = @"
                    INSERT INTO customer
                    (id_user, nama_customer, telepon, email)
                    VALUES
                    (@IdUser, @NamaCustomer, @Telepon, @Email);
                ";

                await connection.ExecuteAsync(
                    insertCustomer,
                    new
                    {
                        IdUser = idUser,
                        request.NamaCustomer,
                        request.Telepon,
                        request.Email
                    },
                    transaction
                );

                await transaction.CommitAsync();

                TempData["Success"] =
                    "Registrasi berhasil. Silakan login.";

                return RedirectToAction("Login");
            }
            catch (MySqlException ex)
            {
                await transaction.RollbackAsync();

                if (ex.Number == 1062)
                {
                    ModelState.AddModelError(
                        "Username",
                        "Username sudah digunakan."
                    );

                    return View(request);
                }

                ModelState.AddModelError(
                    string.Empty,
                    "Registrasi gagal. Silakan coba kembali."
                );

                return View(request);
            }
            catch
            {
                await transaction.RollbackAsync();

                ModelState.AddModelError(
                    string.Empty,
                    "Terjadi kesalahan saat registrasi."
                );

                return View(request);
            }
        }

        // =========================================
        // LOGOUT
        // =========================================
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync("TokoMusikCookie");

            return RedirectToAction("Login", "Auth");
        }

        // =========================================
        // AKSES DITOLAK
        // =========================================
        [AllowAnonymous]
        [HttpGet]
        public IActionResult AccessDenied()
        {
            return View();
        }

        // =========================================
        // MODEL DATA LOGIN INTERNAL
        // =========================================
        private class LoginData
        {
            public int IdUser { get; set; }

            public string Username { get; set; } = "";

            public string PasswordHash { get; set; } = "";

            public string Role { get; set; } = "";

            public string Status { get; set; } = "";

            public int? IdCustomer { get; set; }
        }
    }
}
