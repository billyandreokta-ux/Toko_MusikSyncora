
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
                    COALESCE(pay.status, 'belum_bayar') AS StatusPembayaran,
                    pay.id_payment AS IdPayment,
                    mp.nama_metode AS NamaMetode,
                    pay.tgl_bayar AS TglBayar,
                    pay.jumlah_bayar AS JumlahBayar,
                    pay.bukti_bayar AS BuktiBayar
                FROM pemesanan p
                INNER JOIN customer c
                    ON c.id_customer = p.id_customer
                INNER JOIN users u
                    ON u.id_user = c.id_user
                LEFT JOIN payments pay
                    ON pay.id_payment = (
                        SELECT MAX(px.id_payment)
                        FROM payments px
                        WHERE px.id_pemesanan = p.id_pemesanan
                    )
                LEFT JOIN metode_pembayaran mp
                    ON mp.id_metode = pay.id_metode
                WHERE (@Status IS NULL OR p.status = @Status)
                ORDER BY
                    CASE
                        WHEN p.status = 'menunggu_bayar' THEN 0
                        WHEN p.status = 'diproses' THEN 1
                        WHEN p.status = 'dikirim' THEN 2
                        ELSE 3
                    END,
                    p.tgl_pesan DESC";

            var data = await connection.QueryAsync<AdminPesananGabunganViewModel>(
                sql,
                new
                {
                    Status = string.IsNullOrWhiteSpace(status) ? null : status
                });

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

                    pay.id_payment AS IdPayment,
                    COALESCE(pay.status, 'belum_bayar') AS StatusPembayaran,
                    mp.nama_metode AS NamaMetode,
                    pay.tgl_bayar AS TglBayar,
                    pay.jumlah_bayar AS JumlahBayar,
                    pay.bukti_bayar AS BuktiBayar,

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
                    ON pay.id_payment = (
                        SELECT MAX(px.id_payment)
                        FROM payments px
                        WHERE px.id_pemesanan = p.id_pemesanan
                    )

                LEFT JOIN metode_pembayaran mp
                    ON mp.id_metode = pay.id_metode

                LEFT JOIN alamat_customer ac
                    ON ac.id_alamat = p.id_alamat

                LEFT JOIN pengiriman pg
                    ON pg.id_pemesanan = p.id_pemesanan

                WHERE p.id_pemesanan = @Id
                LIMIT 1";

            var pesanan = await connection
                .QueryFirstOrDefaultAsync<AdminPesananGabunganDetailViewModel>(
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

            pesanan.Items = (
                await connection.QueryAsync<AdminPesananGabunganItemViewModel>(
                    sqlItems,
                    new { Id = id }
                )
            ).ToList();

            return View(pesanan);
        }

        [HttpPost("VerifikasiPembayaran/{idPayment:int}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VerifikasiPembayaran(
            int idPayment,
            string keputusan)
        {
            if (keputusan != "berhasil" && keputusan != "gagal")
            {
                TempData["Error"] = "Keputusan verifikasi tidak valid.";
                return RedirectToAction(nameof(Index));
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
                        pay.bukti_bayar AS BuktiBayar,
                        p.status AS StatusPesanan
                    FROM payments pay
                    INNER JOIN pemesanan p
                        ON p.id_pemesanan = pay.id_pemesanan
                    WHERE pay.id_payment = @IdPayment
                    LIMIT 1
                    FOR UPDATE";

                var data = await connection.QueryFirstOrDefaultAsync<VerifikasiData>(
                    sqlCek,
                    new { IdPayment = idPayment },
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
                        "Pembayaran tidak dapat diverifikasi. Pastikan pesanan masih menunggu pembayaran dan bukti sudah diunggah.";

                    return RedirectToAction(nameof(Detail), new
                    {
                        id = data.IdPemesanan
                    });
                }

                const string sqlUpdatePayment = @"
                    UPDATE payments
                    SET status = @Status
                    WHERE id_payment = @IdPayment
                      AND status = 'pending'";

                int paymentUpdated = await connection.ExecuteAsync(
                    sqlUpdatePayment,
                    new
                    {
                        Status = keputusan,
                        IdPayment = idPayment
                    },
                    transaction);

                if (paymentUpdated != 1)
                {
                    await transaction.RollbackAsync();

                    TempData["Error"] =
                        "Pembayaran sudah berubah atau sedang diproses oleh admin lain.";

                    return RedirectToAction(nameof(Detail), new
                    {
                        id = data.IdPemesanan
                    });
                }

                if (keputusan == "berhasil")
                {
                    const string sqlUpdatePesanan = @"
                        UPDATE pemesanan
                        SET status = 'diproses'
                        WHERE id_pemesanan = @IdPemesanan
                          AND status = 'menunggu_bayar'";

                    int pesananUpdated = await connection.ExecuteAsync(
                        sqlUpdatePesanan,
                        new { data.IdPemesanan },
                        transaction);

                    if (pesananUpdated != 1)
                    {
                        throw new InvalidOperationException(
                            "Status pesanan tidak dapat diperbarui menjadi diproses."
                        );
                    }
                }

                await transaction.CommitAsync();

                TempData["Success"] = keputusan == "berhasil"
                    ? "Pembayaran berhasil diverifikasi. Status pesanan otomatis berubah menjadi diproses."
                    : "Pembayaran ditolak. Pelanggan dapat mengunggah bukti pembayaran kembali.";

                return RedirectToAction(nameof(Detail), new
                {
                    id = data.IdPemesanan
                });
            }
            catch
            {
                await transaction.RollbackAsync();

                TempData["Error"] =
                    "Terjadi kesalahan saat memproses verifikasi pembayaran.";

                return RedirectToAction(nameof(Index));
            }
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

        private class VerifikasiData
        {
            public int IdPemesanan { get; set; }
            public string StatusPembayaran { get; set; } = "";
            public string StatusPesanan { get; set; } = "";
            public string? BuktiBayar { get; set; }
        }
    }
}
