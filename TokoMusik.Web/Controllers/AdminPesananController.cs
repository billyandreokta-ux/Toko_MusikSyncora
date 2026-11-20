
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MySqlConnector;
using TokoMusik.Web.Data;
using TokoMusik.Web.Models;

namespace TokoMusik.Web.Controllers
{
    [Authorize(Roles = "admin")]
    [Route("AdminPesanan")]
    public class AdminPesananController : Controller
    {
        private readonly Koneksi _koneksi;

        public AdminPesananController(Koneksi koneksi)
        {
            _koneksi = koneksi;
        }

        [HttpGet("")]
        public async Task<IActionResult> Index(string? status)
        {
            await using var connection = _koneksi.GetConnection();
            await connection.OpenAsync();

            const string sql = @"
                SELECT
                    p.id_pemesanan AS IdPemesanan,
                    c.nama_customer AS NamaCustomer,
                    u.username AS Username,
                    p.tgl_pesan AS TglPesan,
                    p.total_bayar AS TotalBayar,
                    p.status AS Status,
                    COALESCE(pay.status, 'belum_bayar') AS StatusPembayaran
                FROM pemesanan p
                INNER JOIN customer c
                    ON c.id_customer = p.id_customer
                INNER JOIN users u
                    ON u.id_user = c.id_user
                LEFT JOIN payments pay
                    ON pay.id_pemesanan = p.id_pemesanan
                WHERE (@Status IS NULL OR p.status = @Status)
                ORDER BY p.tgl_pesan DESC";

            var data = await connection.QueryAsync<AdminPesananViewModel>(
                sql,
                new { Status = string.IsNullOrWhiteSpace(status) ? null : status });

            ViewBag.StatusFilter = status;

            return View(data.ToList());
        }

        [HttpGet("Detail/{id:int}")]
        public async Task<IActionResult> Detail(int id)
        {
            await using var connection = _koneksi.GetConnection();
            await connection.OpenAsync();

            const string sqlPesanan = @"
                SELECT
                    p.id_pemesanan AS IdPemesanan,
                    c.nama_customer AS NamaCustomer,
                    u.username AS Username,
                    c.telepon AS Telepon,
                    p.tgl_pesan AS TglPesan,
                    p.subtotal AS Subtotal,
                    p.diskon AS Diskon,
                    p.ongkir AS Ongkir,
                    p.total_bayar AS TotalBayar,
                    p.status AS Status,
                    COALESCE(pay.status, 'belum_bayar') AS StatusPembayaran,
                    COALESCE(ac.penerima, '-') AS NamaPenerima,
                    COALESCE(ac.telepon, '-') AS TeleponPenerima,
                    COALESCE(ac.alamat_lengkap, '-') AS AlamatLengkap,
                    COALESCE(ac.kota, '-') AS Kota,
                    COALESCE(ac.provinsi, '-') AS Provinsi,
                    COALESCE(ac.kode_pos, '-') AS KodePos,
                    pg.kurir AS Kurir,
                    pg.no_resi AS NoResi,
                    pg.status AS StatusPengiriman
                FROM pemesanan p
                INNER JOIN customer c
                    ON c.id_customer = p.id_customer
                INNER JOIN users u
                    ON u.id_user = c.id_user
                LEFT JOIN payments pay
                    ON pay.id_pemesanan = p.id_pemesanan
                LEFT JOIN alamat_customer ac
                    ON ac.id_alamat = p.id_alamat
                LEFT JOIN pengiriman pg
                    ON pg.id_pemesanan = p.id_pemesanan
                WHERE p.id_pemesanan = @Id
                LIMIT 1";

            var pesanan = await connection.QueryFirstOrDefaultAsync<AdminPesananDetailViewModel>(
                sqlPesanan,
                new { Id = id });

            if (pesanan == null)
                return NotFound();

            const string sqlItems = @"
                SELECT
                    pr.nama_produk AS NamaProduk,
                    pr.kode_produk AS KodeProduk,
                    dp.jumlah AS Jumlah,
                    dp.harga_satuan AS HargaSatuan,
                    dp.subtotal AS Subtotal
                FROM detail_pesanan dp
                INNER JOIN produk pr
                    ON pr.id_produk = dp.id_produk
                WHERE dp.id_pemesanan = @Id";

            pesanan.Items = (await connection.QueryAsync<AdminPesananItemViewModel>(
                sqlItems,
                new { Id = id })).ToList();

            return View(pesanan);
        }

        [HttpPost("UbahStatus/{id:int}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UbahStatus(
            int id,
            string statusBaru,
            string? kurir,
            string? noResi)
        {
            var statusDiizinkan = new[]
            {
                "diproses",
                "dikirim",
                "selesai"
            };

            if (!statusDiizinkan.Contains(statusBaru))
            {
                TempData["Error"] = "Status pesanan tidak valid.";
                return RedirectToAction(nameof(Detail), new { id });
            }

            if (statusBaru == "dikirim" &&
                (string.IsNullOrWhiteSpace(kurir) ||
                 string.IsNullOrWhiteSpace(noResi)))
            {
                TempData["Error"] =
                    "Nama kurir dan nomor resi wajib diisi sebelum pesanan dikirim.";

                return RedirectToAction(nameof(Detail), new { id });
            }

            await using var connection = _koneksi.GetConnection();
            await connection.OpenAsync();

            await using var transaction =
                await connection.BeginTransactionAsync();

            try
            {
                const string sqlCek = @"
                    SELECT status
                    FROM pemesanan
                    WHERE id_pemesanan = @Id
                    FOR UPDATE";

                string? statusLama =
                    await connection.QueryFirstOrDefaultAsync<string>(
                        sqlCek,
                        new { Id = id },
                        transaction);

                if (statusLama == null)
                {
                    await transaction.RollbackAsync();
                    return NotFound();
                }

                bool transisiValid =
                    (statusLama == "dibayar" && statusBaru == "diproses") ||
                    (statusLama == "diproses" && statusBaru == "dikirim") ||
                    (statusLama == "dikirim" && statusBaru == "selesai");

                if (!transisiValid)
                {
                    await transaction.RollbackAsync();

                    TempData["Error"] =
                        $"Status tidak dapat diubah dari {statusLama} menjadi {statusBaru}.";

                    return RedirectToAction(nameof(Detail), new { id });
                }

                if (statusBaru == "dikirim")
                {
                    const string sqlPengiriman = @"
                        INSERT INTO pengiriman
                            (id_pemesanan, kurir, no_resi, tgl_kirim, status)
                        VALUES
                            (@Id, @Kurir, @NoResi, CURRENT_TIMESTAMP, 'dikirim')
                        ON DUPLICATE KEY UPDATE
                            kurir = VALUES(kurir),
                            no_resi = VALUES(no_resi),
                            tgl_kirim = CURRENT_TIMESTAMP,
                            status = 'dikirim'";

                    await connection.ExecuteAsync(
                        sqlPengiriman,
                        new
                        {
                            Id = id,
                            Kurir = kurir!.Trim(),
                            NoResi = noResi!.Trim()
                        },
                        transaction);
                }

                if (statusBaru == "selesai")
                {
                    const string sqlPengiriman = @"
                        UPDATE pengiriman
                        SET status = 'diterima',
                            tgl_terima = CURRENT_TIMESTAMP
                        WHERE id_pemesanan = @Id";

                    int jumlahPengiriman = await connection.ExecuteAsync(
                        sqlPengiriman,
                        new { Id = id },
                        transaction);

                    if (jumlahPengiriman == 0)
                    {
                        await transaction.RollbackAsync();

                        TempData["Error"] =
                            "Data pengiriman belum tersedia. Pesanan tidak dapat diselesaikan.";

                        return RedirectToAction(nameof(Detail), new { id });
                    }
                }

                const string sqlUpdate = @"
                    UPDATE pemesanan
                    SET status = @Status
                    WHERE id_pemesanan = @Id";

                await connection.ExecuteAsync(
                    sqlUpdate,
                    new
                    {
                        Status = statusBaru,
                        Id = id
                    },
                    transaction);

                await transaction.CommitAsync();

                TempData["Success"] =
                    $"Status pesanan berhasil diubah menjadi {statusBaru}.";

                return RedirectToAction(nameof(Detail), new { id });
            }
            catch
            {
                await transaction.RollbackAsync();

                TempData["Error"] =
                    "Terjadi kesalahan saat mengubah status pesanan.";

                return RedirectToAction(nameof(Detail), new { id });
            }
        }
    }
}
