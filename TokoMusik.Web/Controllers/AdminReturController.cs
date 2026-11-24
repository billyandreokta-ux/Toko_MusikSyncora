
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TokoMusik.Web.Data;
using TokoMusik.Web.Models;

namespace TokoMusik.Web.Controllers
{
    [Authorize(Roles = "admin")]
    public class AdminReturController : Controller
    {
        private readonly Koneksi _koneksi;

        public AdminReturController(Koneksi koneksi)
        {
            _koneksi = koneksi;
        }

        public async Task<IActionResult> Index(string? status, string? cari)
        {
            await using var connection = _koneksi.GetConnection();

            var data = await connection.QueryAsync<ReturItemViewModel>(
                @"
                SELECT
                    r.id_return AS IdReturn,
                    r.id_detail_pesanan AS IdDetailPesanan,
                    r.tgl_return AS TanggalReturn,
                    r.tgl_pengajuan AS TanggalPengajuan,
                    dp.id_pemesanan AS IdPemesanan,
                    p.kode_produk AS KodeProduk,
                    p.nama_produk AS NamaProduk,
                    r.jumlah AS Jumlah,
                    r.alasan AS Alasan,
                    r.foto_bukti AS FotoBukti,
                    r.status AS Status,
                    c.nama_customer AS NamaCustomer,
                    r.barang_diterima AS BarangDiterima,
                    r.layak_jual AS LayakJual,
                    r.stok_ditambahkan AS StokDitambahkan,
                    r.tgl_pemeriksaan AS TanggalPemeriksaan,
                    r.catatan_admin AS CatatanAdmin
                FROM return_produk r
                INNER JOIN detail_pesanan dp
                    ON dp.id_detail_pesanan = r.id_detail_pesanan
                INNER JOIN pemesanan pm
                    ON pm.id_pemesanan = dp.id_pemesanan
                INNER JOIN produk p
                    ON p.id_produk = dp.id_produk
                INNER JOIN customer c
                    ON c.id_customer = pm.id_customer
                WHERE (@Status IS NULL OR r.status = @Status)
                  AND (
                      @Cari IS NULL
                      OR p.nama_produk LIKE CONCAT('%', @Cari, '%')
                      OR p.kode_produk LIKE CONCAT('%', @Cari, '%')
                      OR c.nama_customer LIKE CONCAT('%', @Cari, '%')
                      OR CAST(pm.id_pemesanan AS CHAR) LIKE CONCAT('%', @Cari, '%')
                  )
                ORDER BY
                    CASE r.status
                        WHEN 'diajukan' THEN 1
                        WHEN 'disetujui' THEN 2
                        WHEN 'ditolak' THEN 3
                        ELSE 4
                    END,
                    r.tgl_pengajuan DESC",
                new
                {
                    Status = string.IsNullOrWhiteSpace(status) ||
                             status == "semua" ? null : status.Trim(),
                    Cari = string.IsNullOrWhiteSpace(cari) ? null : cari.Trim()
                });

            ViewBag.StatusFilter = status ?? "semua";
            ViewBag.KataKunci = cari ?? "";

            return View(data.ToList());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UbahStatus(int idReturn, string status)
        {
            if (status is not "disetujui" and not "ditolak")
            {
                TempData["Error"] = "Status yang dipilih tidak valid.";
                return RedirectToAction(nameof(Index));
            }

            await using var connection = _koneksi.GetConnection();
            await connection.OpenAsync();

            await using var transaction = await connection.BeginTransactionAsync();

            try
            {
                var retur = await connection.QueryFirstOrDefaultAsync<dynamic>(
                    @"
                    SELECT status
                    FROM return_produk
                    WHERE id_return = @IdReturn
                    FOR UPDATE",
                    new { IdReturn = idReturn },
                    transaction);

                if (retur == null || (string)retur.status != "diajukan")
                {
                    await transaction.RollbackAsync();
                    TempData["Error"] =
                        "Pengajuan tidak ditemukan atau sudah diproses sebelumnya.";

                    return RedirectToAction(nameof(Index));
                }

                await connection.ExecuteAsync(
                    @"
                    UPDATE return_produk
                    SET status = @Status
                    WHERE id_return = @IdReturn
                      AND status = 'diajukan'",
                    new { IdReturn = idReturn, Status = status },
                    transaction);

                await transaction.CommitAsync();

                TempData["Success"] = status == "disetujui"
                    ? "Pengajuan retur disetujui. Tunggu barang retur diterima dan diperiksa."
                    : "Pengajuan retur berhasil ditolak.";

                return RedirectToAction(nameof(Index));
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SelesaikanPemeriksaan(
            int idReturn,
            bool barangDiterima,
            bool layakJual,
            string? catatanAdmin)
        {
            if (!barangDiterima)
            {
                TempData["Error"] =
                    "Retur belum dapat diselesaikan karena barang belum diterima admin.";

                return RedirectToAction(nameof(Index));
            }

            await using var connection = _koneksi.GetConnection();
            await connection.OpenAsync();

            await using var transaction = await connection.BeginTransactionAsync();

            try
            {
                var retur = await connection.QueryFirstOrDefaultAsync<dynamic>(
                    @"
                    SELECT
                        r.id_return,
                        r.id_detail_pesanan,
                        r.jumlah,
                        r.status,
                        r.stok_ditambahkan,
                        dp.id_produk
                    FROM return_produk r
                    INNER JOIN detail_pesanan dp
                        ON dp.id_detail_pesanan = r.id_detail_pesanan
                    WHERE r.id_return = @IdReturn
                    FOR UPDATE",
                    new { IdReturn = idReturn },
                    transaction);

                if (retur == null)
                {
                    await transaction.RollbackAsync();
                    TempData["Error"] = "Data retur tidak ditemukan.";
                    return RedirectToAction(nameof(Index));
                }

                if ((string)retur.status != "disetujui")
                {
                    await transaction.RollbackAsync();
                    TempData["Error"] =
                        "Pemeriksaan hanya dapat dilakukan pada retur yang sudah disetujui.";

                    return RedirectToAction(nameof(Index));
                }

                if (Convert.ToInt32(retur.stok_ditambahkan) == 1)
                {
                    await transaction.RollbackAsync();
                    TempData["Error"] =
                        "Stok retur ini sudah pernah ditambahkan.";

                    return RedirectToAction(nameof(Index));
                }

                int idProduk = Convert.ToInt32(retur.id_produk);
                int jumlah = Convert.ToInt32(retur.jumlah);

                if (layakJual)
                {
                    int stokAda = await connection.ExecuteScalarAsync<int>(
                        @"
                        SELECT COUNT(*)
                        FROM stock
                        WHERE id_produk = @IdProduk
                        FOR UPDATE",
                        new { IdProduk = idProduk },
                        transaction);

                    if (stokAda == 0)
                    {
                        await transaction.RollbackAsync();
                        TempData["Error"] =
                            "Data stok produk tidak ditemukan. Stok tidak ditambahkan.";

                        return RedirectToAction(nameof(Index));
                    }

                    await connection.ExecuteAsync(
                        @"
                        UPDATE stock
                        SET jumlah = jumlah + @Jumlah,
                            updated_at = CURRENT_TIMESTAMP
                        WHERE id_produk = @IdProduk",
                        new { IdProduk = idProduk, Jumlah = jumlah },
                        transaction);
                }

                await connection.ExecuteAsync(
                    @"
                    UPDATE return_produk
                    SET
                        status = 'selesai',
                        barang_diterima = 1,
                        layak_jual = @LayakJual,
                        stok_ditambahkan = @StokDitambahkan,
                        tgl_pemeriksaan = CURRENT_TIMESTAMP,
                        catatan_admin = @CatatanAdmin
                    WHERE id_return = @IdReturn
                      AND status = 'disetujui'
                      AND stok_ditambahkan = 0",
                    new
                    {
                        IdReturn = idReturn,
                        LayakJual = layakJual ? 1 : 0,
                        StokDitambahkan = layakJual ? 1 : 0,
                        CatatanAdmin = string.IsNullOrWhiteSpace(catatanAdmin)
                            ? null : catatanAdmin.Trim()
                    },
                    transaction);

                await transaction.CommitAsync();

                TempData["Success"] = layakJual
                    ? "Retur selesai. Barang dinyatakan layak jual dan stok berhasil ditambahkan."
                    : "Retur selesai. Barang dinyatakan tidak layak jual sehingga stok tidak ditambahkan.";

                return RedirectToAction(nameof(Index));
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
    }
}
