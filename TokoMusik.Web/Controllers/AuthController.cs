
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Dapper;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MySqlConnector;
using TokoMusik.Web.Data;
using TokoMusik.Web.DTOs;
using TokoMusik.Web.Models;

namespace TokoMusik.Web.Controllers
{
    public class AuthController : Controller
    {
        private readonly Koneksi _koneksi;

        private const string CookieScheme = "TokoMusikCookie";
        private const string DeviceCookieName = "Syncora.Device";

        public AuthController(Koneksi koneksi)
        {
            _koneksi = koneksi;
        }

        private static string HashToken(string token)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
            return Convert.ToHexString(bytes);
        }

        private string AmbilAtauBuatTokenPerangkat()
        {
            if (Request.Cookies.TryGetValue(
                    DeviceCookieName, out var token) &&
                !string.IsNullOrWhiteSpace(token) &&
                token.Length == 64 &&
                token.All(Uri.IsHexDigit))
            {
                return token;
            }

            token = Convert.ToHexString(
                RandomNumberGenerator.GetBytes(32));

            Response.Cookies.Append(
                DeviceCookieName,
                token,
                new CookieOptions
                {
                    HttpOnly = true,
                    Secure = Request.IsHttps,
                    SameSite = SameSiteMode.Lax,
                    IsEssential = true,
                    Path = "/",
                    Expires = DateTimeOffset.UtcNow.AddDays(180)
                });

            return token;
        }

        private string? AmbilTokenPerangkat()
        {
            if (Request.Cookies.TryGetValue(
                    DeviceCookieName, out var token) &&
                !string.IsNullOrWhiteSpace(token) &&
                token.Length == 64 &&
                token.All(Uri.IsHexDigit))
            {
                return token;
            }

            return null;
        }

        private static ClaimsPrincipal BuatPrincipal(LoginData account)
        {
            var claims = new List<Claim>
            {
                new Claim(
                    ClaimTypes.NameIdentifier,
                    account.IdUser.ToString()),

                new Claim(
                    ClaimTypes.Name,
                    account.Username),

                new Claim(
                    ClaimTypes.Role,
                    account.Role)
            };

            if (account.IdCustomer.HasValue)
            {
                claims.Add(new Claim(
                    "IdCustomer",
                    account.IdCustomer.Value.ToString()));
            }

            if (!string.IsNullOrWhiteSpace(account.FotoProfil))
            {
                claims.Add(new Claim(
                    "FotoProfil",
                    account.FotoProfil));
            }

            var identity = new ClaimsIdentity(
                claims,
                CookieScheme);

            return new ClaimsPrincipal(identity);
        }

        private async Task LoginAkunAsync(LoginData account)
        {
            var principal = BuatPrincipal(account);

            await HttpContext.SignInAsync(
                CookieScheme,
                principal);
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
                    c.id_customer AS IdCustomer,
                    c.foto_profil AS FotoProfil
                FROM users u
                LEFT JOIN customer c
                    ON c.id_user = u.id_user
                WHERE u.username = @Username
                LIMIT 1;";

            var account =
                await connection.QuerySingleOrDefaultAsync<LoginData>(
                    sql,
                    new { request.Username });

            bool roleValid =
                account != null &&
                (account.Role == "admin" || account.Role == "user");

            bool passwordValid =
                account != null &&
                !string.IsNullOrWhiteSpace(account.PasswordHash) &&
                !string.IsNullOrEmpty(request.Password) &&
                BCrypt.Net.BCrypt.Verify(
                    request.Password,
                    account.PasswordHash);

            if (account == null ||
                account.Status != "aktif" ||
                !roleValid ||
                !passwordValid)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "Username atau password tidak valid.");

                return View(request);
            }

            // Simpan atau perbarui akun pada perangkat ini.
            // Akun lain yang sudah tersimpan tidak dihapus.
            var token = AmbilAtauBuatTokenPerangkat();
            var tokenHash = HashToken(token);

            const string simpanPerangkat = @"
                INSERT INTO akun_perangkat
                    (id_user, token_hash, created_at, last_used)
                VALUES
                    (@IdUser, @TokenHash, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP)
                ON DUPLICATE KEY UPDATE
                    last_used = CURRENT_TIMESTAMP;";

            await connection.ExecuteAsync(
                simpanPerangkat,
                new
                {
                    account.IdUser,
                    TokenHash = tokenHash
                });

            await LoginAkunAsync(account);

            if (account.Role == "admin")
            {
                return RedirectToAction("Index", "Admin");
            }

            return RedirectToAction("Index", "User");
        }

        // =========================================
        // DAFTAR AKUN YANG PERNAH LOGIN
        // AKUN AKTIF TIDAK DITAMPILKAN
        // =========================================

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> PilihAkun()
        {
            var token = AmbilTokenPerangkat();

            if (token == null)
            {
                return RedirectToAction(nameof(Login));
            }

            var idUserAktif = User.FindFirstValue(
                ClaimTypes.NameIdentifier);

            if (!int.TryParse(idUserAktif, out int currentIdUser))
            {
                return RedirectToAction(nameof(Login));
            }

            using var connection = _koneksi.GetConnection();
            await connection.OpenAsync();

            const string sql = @"
                SELECT
                    u.id_user AS IdUser,
                    u.username AS Username,
                    u.role AS Role,
                    c.nama_customer AS NamaCustomer,
                    c.foto_profil AS Foto
                FROM akun_perangkat ap
                INNER JOIN users u
                    ON u.id_user = ap.id_user
                LEFT JOIN customer c
                    ON c.id_user = u.id_user
                WHERE ap.token_hash = @TokenHash
                  AND u.status = 'aktif'
                  AND u.id_user <> @CurrentIdUser
                ORDER BY ap.last_used DESC;";

            var daftarAkun =
                (await connection.QueryAsync<AkunTersimpanViewModel>(
                    sql,
                    new
                    {
                        TokenHash = HashToken(token),
                        CurrentIdUser = currentIdUser
                    })).ToList();

            return View(daftarAkun);
        }

        // =========================================
        // GANTI AKUN TANPA PASSWORD
        // =========================================

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GantiAkun(int idUser)
        {
            var token = AmbilTokenPerangkat();

            if (token == null)
            {
                await HttpContext.SignOutAsync(CookieScheme);
                return RedirectToAction(nameof(Login));
            }

            var idUserAktif = User.FindFirstValue(
                ClaimTypes.NameIdentifier);

            if (!int.TryParse(idUserAktif, out int currentIdUser))
            {
                await HttpContext.SignOutAsync(CookieScheme);
                return RedirectToAction(nameof(Login));
            }

            // Mencegah akun aktif dipilih kembali melalui request langsung.
            if (idUser == currentIdUser)
            {
                return RedirectToAction(nameof(PilihAkun));
            }

            using var connection = _koneksi.GetConnection();
            await connection.OpenAsync();

            const string sql = @"
                SELECT
                    u.id_user AS IdUser,
                    u.username AS Username,
                    u.role AS Role,
                    u.status AS Status,
                    c.id_customer AS IdCustomer,
                    c.foto_profil AS FotoProfil
                FROM akun_perangkat ap
                INNER JOIN users u
                    ON u.id_user = ap.id_user
                LEFT JOIN customer c
                    ON c.id_user = u.id_user
                WHERE ap.id_user = @IdUser
                  AND ap.token_hash = @TokenHash
                  AND u.status = 'aktif'
                  AND u.role IN ('admin', 'user')
                LIMIT 1;";

            var account = await connection.QueryFirstOrDefaultAsync<LoginData>(
                sql,
                new
                {
                    IdUser = idUser,
                    TokenHash = HashToken(token)
                });

            if (account == null)
            {
                TempData["Error"] =
                    "Akun tidak tersedia pada perangkat ini.";

                return RedirectToAction(nameof(PilihAkun));
            }

            const string updateLastUsed = @"
                UPDATE akun_perangkat
                SET last_used = CURRENT_TIMESTAMP
                WHERE id_user = @IdUser
                  AND token_hash = @TokenHash;";

            await connection.ExecuteAsync(
                updateLastUsed,
                new
                {
                    IdUser = idUser,
                    TokenHash = HashToken(token)
                });

            await LoginAkunAsync(account);

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

            await using var transaction =
                await connection.BeginTransactionAsync();

            try
            {
                const string cekUsername = @"
                    SELECT COUNT(*)
                    FROM users
                    WHERE username = @Username;";

                int jumlahUsername =
                    await connection.ExecuteScalarAsync<int>(
                        cekUsername,
                        new { request.Username },
                        transaction);

                if (jumlahUsername > 0)
                {
                    ModelState.AddModelError(
                        "Username",
                        "Username sudah digunakan.");

                    await transaction.RollbackAsync();
                    return View(request);
                }

                string passwordHash =
                    BCrypt.Net.BCrypt.HashPassword(request.Password);

                const string insertUser = @"
                    INSERT INTO users
                        (username, password_hash, role, status)
                    VALUES
                        (@Username, @PasswordHash, 'user', 'aktif');

                    SELECT LAST_INSERT_ID();";

                int idUser =
                    await connection.ExecuteScalarAsync<int>(
                        insertUser,
                        new
                        {
                            request.Username,
                            PasswordHash = passwordHash
                        },
                        transaction);

                const string insertCustomer = @"
                    INSERT INTO customer
                        (id_user, nama_customer, telepon, email)
                    VALUES
                        (@IdUser, @NamaCustomer, @Telepon, @Email);";

                await connection.ExecuteAsync(
                    insertCustomer,
                    new
                    {
                        IdUser = idUser,
                        request.NamaCustomer,
                        request.Telepon,
                        request.Email
                    },
                    transaction);

                await transaction.CommitAsync();

                TempData["Success"] =
                    "Registrasi berhasil. Silakan login.";

                return RedirectToAction(nameof(Login));
            }
            catch (MySqlException ex)
            {
                await RollbackSafelyAsync(transaction);

                if (ex.Number == 1062)
                {
                    ModelState.AddModelError(
                        "Username",
                        "Username atau data akun sudah digunakan.");

                    return View(request);
                }

                ModelState.AddModelError(
                    string.Empty,
                    "Registrasi gagal. Silakan coba kembali.");

                return View(request);
            }
            catch
            {
                await RollbackSafelyAsync(transaction);

                ModelState.AddModelError(
                    string.Empty,
                    "Terjadi kesalahan saat registrasi.");

                return View(request);
            }
        }

        // =========================================
        // LOGOUT
        // HANYA MENGHAPUS AKUN YANG SEDANG AKTIF
        // AKUN LAIN TETAP TERSIMPAN
        // =========================================

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            var token = AmbilTokenPerangkat();

            var idUserAktif = User.FindFirstValue(
                ClaimTypes.NameIdentifier);

            // Hapus hanya akun yang sedang digunakan.
            // Jangan menghapus akun lain pada perangkat ini.
            if (!string.IsNullOrWhiteSpace(token) &&
                int.TryParse(idUserAktif, out int currentIdUser))
            {
                using var connection = _koneksi.GetConnection();
                await connection.OpenAsync();

                const string hapusAkunAktif = @"
                    DELETE FROM akun_perangkat
                    WHERE token_hash = @TokenHash
                      AND id_user = @IdUser;";

                await connection.ExecuteAsync(
                    hapusAkunAktif,
                    new
                    {
                        TokenHash = HashToken(token),
                        IdUser = currentIdUser
                    });
            }

            // Akhiri sesi akun yang sedang aktif.
            await HttpContext.SignOutAsync(CookieScheme);

            // Cookie perangkat TIDAK dihapus.
            // Dengan demikian, akun lain yang tersimpan
            // tetap dapat dipilih setelah login kembali.

            return RedirectToAction(nameof(Login));
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
        // ROLLBACK TRANSAKSI SECARA AMAN
        // =========================================

        private static async Task RollbackSafelyAsync(
            System.Data.Common.DbTransaction transaction)
        {
            try
            {
                await transaction.RollbackAsync();
            }
            catch
            {
                // Menghindari error tambahan jika transaksi
                // sudah dibatalkan atau koneksi terputus.
            }
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

            public string? FotoProfil { get; set; }
        }
    }
}
