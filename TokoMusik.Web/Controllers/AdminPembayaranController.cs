
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TokoMusik.Web.Data;
using TokoMusik.Web.Models;

namespace TokoMusik.Web.Controllers
{
    [Authorize(Roles = "admin")]
    [Route("AdminPembayaran")]
    public class AdminPembayaranController : Controller
    {
        private readonly Koneksi _koneksi;

        public AdminPembayaranController(Koneksi koneksi)
        {
            _koneksi = koneksi;
        }

        [HttpGet("")]
        public async Task<IActionResult> Index()
        {
            await using var connection = _koneksi.GetConnection();
            await connection.OpenAsync();

            const string sql = @"
                SELECT
                    pay.id_payment AS IdPayment,
                    p.id_pemesanan AS IdPemesanan,
                    u.username AS Username,
                    c.nama_customer AS NamaCustomer,
                    mp.nama_metode AS NamaMetode,
                    pay.tgl_bayar AS TglBayar,
                    pay.jumlah_bayar AS JumlahBayar,
                    pay.bukti_bayar AS BuktiBayar,
                    pay.status AS StatusPembayaran,
                    p.status AS StatusPesanan
                FROM payments pay
                INNER JOIN pemesanan p
                    ON p.id_pemesanan = pay.id_pemesanan
                INNER JOIN customer c
                    ON c.id_customer = p.id_customer
                INNER JOIN users u
                    ON u.id_user = c.id_user
                INNER JOIN metode_pembayaran mp
                    ON mp.id_metode = pay.id_metode
                ORDER BY
                    CASE WHEN pay.status = 'pending' THEN 0 ELSE 1 END,
                    pay.tgl_bayar DESC,
                    pay.id_payment DESC";

            var data =
                await connection.QueryAsync<AdminPembayaranViewModel>(sql);

            return View(data.ToList());
        }

        [HttpGet("Detail/{id:int}")]
        public async Task<IActionResult> Detail(int id)
        {
            await using var connection = _koneksi.GetConnection();
            await connection.OpenAsync();

            const string sql = @"
                SELECT
                    pay.id_payment AS IdPayment,
                    p.id_pemesanan AS IdPemesanan,
                    u.username AS Username,
                    c.nama_customer AS NamaCustomer,
                    c.telepon AS Telepon,
                    mp.nama_metode AS NamaMetode,
                    pay.tgl_bayar AS TglBayar,
                    pay.jumlah_bayar AS JumlahBayar,
                    pay.bukti_bayar AS BuktiBayar,
                    pay.status AS StatusPembayaran,
                    p.status AS StatusPesanan,
                    p.total_bayar AS TotalPesanan
                FROM payments pay
                INNER JOIN pemesanan p
                    ON p.id_pemesanan = pay.id_pemesanan
                INNER JOIN customer c
                    ON c.id_customer = p.id_customer
                INNER JOIN users u
                    ON u.id_user = c.id_user
                INNER JOIN metode_pembayaran mp
                    ON mp.id_metode = pay.id_metode
                WHERE pay.id_payment = @IdPayment
                LIMIT 1";

            var data =
                await connection.QueryFirstOrDefaultAsync<AdminPembayaranDetailViewModel>(
                    sql,
                    new { IdPayment = id });

            if (data == null)
            {
                return NotFound();
            }

            return View(data);
        }

        [HttpPost("Verifikasi/{id:int}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Verifikasi(
            int id,
            string keputusan)
        {
            if (keputusan != "berhasil" && keputusan != "gagal")
            {
                TempData["Error"] = "Keputusan verifikasi tidak valid.";
                return RedirectToAction(nameof(Detail), new { id });
            }

            await using var connection = _koneksi.GetConnection();
            await connection.OpenAsync();

            await using var transaction =
                await connection.BeginTransactionAsync();

            try
            {
                const string sqlCek = @"
                    SELECT
                        pay.id_pemesanan AS IdPemesanan,
                        pay.status AS StatusPembayaran,
                        p.status AS StatusPesanan,
                        pay.bukti_bayar AS BuktiBayar,
                        pay.jumlah_bayar AS JumlahBayar,
                        p.total_bayar AS TotalPesanan
                    FROM payments pay
                    INNER JOIN pemesanan p
                        ON p.id_pemesanan = pay.id_pemesanan
                    WHERE pay.id_payment = @IdPayment
                    LIMIT 1
                    FOR UPDATE";

                var data =
                    await connection.QueryFirstOrDefaultAsync<VerifikasiData>(
                        sqlCek,
                        new { IdPayment = id },
                        transaction);

                if (data == null)
                {
                    await transaction.RollbackAsync();
                    return NotFound();
                }

                if (data.StatusPesanan != "menunggu_bayar" ||
                    data.StatusPembayaran != "pending" ||
                    string.IsNullOrWhiteSpace(data.BuktiBayar))
                {
                    await transaction.RollbackAsync();

                    TempData["Error"] =
                        "Pembayaran ini tidak dapat diverifikasi. Periksa status dan bukti pembayarannya.";

                    return RedirectToAction(nameof(Detail), new { id });
                }

                if (keputusan == "berhasil" &&
                    data.JumlahBayar != data.TotalPesanan)
                {
                    await transaction.RollbackAsync();

                    TempData["Error"] =
                        "Nominal pembayaran tidak sama dengan total pesanan. Pembayaran belum dapat diterima.";

                    return RedirectToAction(nameof(Detail), new { id });
                }

                const string sqlUpdatePayment = @"
                    UPDATE payments
                    SET status = @Status
                    WHERE id_payment = @IdPayment
                      AND status = 'pending'";

                int pembayaranDiubah = await connection.ExecuteAsync(
                    sqlUpdatePayment,
                    new
                    {
                        Status = keputusan,
                        IdPayment = id
                    },
                    transaction);

                if (pembayaranDiubah == 0)
                {
                    await transaction.RollbackAsync();

                    TempData["Error"] =
                        "Status pembayaran sudah berubah. Muat ulang halaman dan periksa kembali.";

                    return RedirectToAction(nameof(Detail), new { id });
                }

                if (keputusan == "berhasil")
                {
                    const string sqlUpdatePesanan = @"
                        UPDATE pemesanan
                        SET status = 'dibayar'
                        WHERE id_pemesanan = @IdPemesanan
                          AND status = 'menunggu_bayar'";

                    int pesananDiubah = await connection.ExecuteAsync(
                        sqlUpdatePesanan,
                        new { data.IdPemesanan },
                        transaction);

                    if (pesananDiubah == 0)
                    {
                        throw new InvalidOperationException(
                            "Status pesanan tidak dapat diperbarui.");
                    }
                }

                await transaction.CommitAsync();

                TempData["Success"] = keputusan == "berhasil"
                    ? "Pembayaran berhasil diverifikasi. Status pesanan telah berubah menjadi dibayar."
                    : "Pembayaran ditolak. Pelanggan dapat mengunggah bukti pembayaran kembali.";

                return RedirectToAction(nameof(Detail), new { id });
            }
            catch
            {
                await transaction.RollbackAsync();

                TempData["Error"] =
                    "Terjadi kesalahan ketika memproses verifikasi pembayaran.";

                return RedirectToAction(nameof(Detail), new { id });
            }
        }

        private class VerifikasiData
        {
            public int IdPemesanan { get; set; }
            public string StatusPembayaran { get; set; } = "";
            public string StatusPesanan { get; set; } = "";
            public string? BuktiBayar { get; set; }
            public decimal JumlahBayar { get; set; }
            public decimal TotalPesanan { get; set; }
        }
    }
}
