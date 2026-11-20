
using System.Security.Claims;
using Dapper;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MySqlConnector;
using TokoMusik.Web.Data;
using TokoMusik.Web.DTOs;

namespace TokoMusik.Web.Controllers.Api
{
    [ApiController]
    [Route("api/auth")]
    public class AuthApiController : ControllerBase
    {
        private readonly Koneksi _koneksi;

        public AuthApiController(Koneksi koneksi)
        {
            _koneksi = koneksi;
        }

        // POST: /api/auth/login
        [AllowAnonymous]
        [HttpPost("login")]
        public async Task<IActionResult> Login(LoginRequest request)
        {
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
                return Unauthorized(new
                {
                    success = false,
                    message = "Username atau password tidak valid."
                });
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

            return Ok(new
            {
                success = true,
                message = "Login berhasil.",
                data = new
                {
                    account.IdUser,
                    account.Username,
                    account.Role,
                    account.IdCustomer
                }
            });
        }

        // POST: /api/auth/register
        [AllowAnonymous]
        [HttpPost("register")]
        public async Task<IActionResult> Register(RegisterRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Data registrasi tidak valid.",
                    errors = ModelState
                });
            }

            using var connection = _koneksi.GetConnection();
            await connection.OpenAsync();

            using var transaction = await connection.BeginTransactionAsync();

            try
            {
                const string checkSql = @"
                    SELECT COUNT(*)
                    FROM users
                    WHERE username = @Username;
                ";

                int exists = await connection.ExecuteScalarAsync<int>(
                    checkSql,
                    new { request.Username },
                    transaction
                );

                if (exists > 0)
                {
                    await transaction.RollbackAsync();

                    return Conflict(new
                    {
                        success = false,
                        message = "Username sudah digunakan."
                    });
                }

                string passwordHash =
                    global::BCrypt.Net.BCrypt.HashPassword(
                        request.Password
                    );

                const string insertUserSql = @"
                    INSERT INTO users
                        (username, password_hash, role, status)
                    VALUES
                        (@Username, @PasswordHash, 'user', 'aktif');

                    SELECT LAST_INSERT_ID();
                ";

                int idUser = await connection.ExecuteScalarAsync<int>(
                    insertUserSql,
                    new
                    {
                        request.Username,
                        PasswordHash = passwordHash
                    },
                    transaction
                );

                const string insertCustomerSql = @"
                    INSERT INTO customer
                        (id_user, nama_customer, telepon, email)
                    VALUES
                        (@IdUser, @NamaCustomer, @Telepon, @Email);
                ";

                await connection.ExecuteAsync(
                    insertCustomerSql,
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

                return Ok(new
                {
                    success = true,
                    message = "Registrasi berhasil.",
                    data = new
                    {
                        IdUser = idUser,
                        request.Username
                    }
                });
            }
            catch (MySqlException ex)
            {
                await transaction.RollbackAsync();

                if (ex.Number == 1062)
                {
                    return Conflict(new
                    {
                        success = false,
                        message = "Username atau data unik lainnya sudah digunakan."
                    });
                }

                return StatusCode(500, new
                {
                    success = false,
                    message = "Registrasi gagal."
                });
            }
            catch
            {
                await transaction.RollbackAsync();

                return StatusCode(500, new
                {
                    success = false,
                    message = "Terjadi kesalahan pada server."
                });
            }
        }

        // POST: /api/auth/logout
        [Authorize]
        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync("TokoMusikCookie");

            return Ok(new
            {
                success = true,
                message = "Logout berhasil."
            });
        }

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
