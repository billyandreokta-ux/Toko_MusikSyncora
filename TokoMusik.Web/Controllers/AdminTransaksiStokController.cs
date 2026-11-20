
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TokoMusik.Web.Data;
using TokoMusik.Web.Models;

namespace TokoMusik.Web.Controllers
{
    [Authorize(Roles = "admin")]
    public class AdminTransaksiStokController : Controller
    {
        private readonly Koneksi _koneksi;

        public AdminTransaksiStokController(Koneksi koneksi)
        {
            _koneksi = koneksi;
        }

        [HttpGet]
        public IActionResult Index()
        {
            return RedirectToAction(nameof(Riwayat));
        }

        // =========================
        // RESTOCK / STOK MASUK
        // =========================

        [HttpGet]
        public async Task<IActionResult> Restock()
        {
            var model = new TransaksiStokViewModel();

            await IsiPilihanRestockAsync(model);

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Restock(TransaksiStokViewModel model)
        {
            if (!ModelState.IsValid)
            {
                await IsiPilihanRestockAsync(model);
                return View(model);
            }

            using var connection = _koneksi.GetConnection();
            await connection.OpenAsync();

            using var transaction = await connection.BeginTransactionAsync();

            try
            {
                // Pastikan produk tersedia dan masih aktif.
                var produkAda = await connection.ExecuteScalarAsync<int>(
                    @"SELECT COUNT(*)
                      FROM produk
                      WHERE id_produk = @IdProduk
                        AND status = 'aktif'",
                    new { model.IdProduk },
                    transaction);

                if (produkAda == 0)
                {
                    await transaction.RollbackAsync();

                    ModelState.AddModelError(
                        "",
                        "Produk tidak ditemukan atau sudah nonaktif.");

                    await IsiPilihanRestockAsync(model);
                    return View(model);
                }

                // Pastikan supplier benar-benar tersedia.
                var supplierAda = await connection.ExecuteScalarAsync<int>(
                    @"SELECT COUNT(*)
                      FROM supplier
                      WHERE id_supplier = @IdSupplier",
                    new { model.IdSupplier },
                    transaction);

                if (supplierAda == 0)
                {
                    await transaction.RollbackAsync();

                    ModelState.AddModelError(
                        "",
                        "Supplier tidak ditemukan. Silakan pilih supplier yang tersedia.");

                    await IsiPilihanRestockAsync(model);
                    return View(model);
                }

                // Catat pembelian stok.
                // Kolom total merupakan generated column sehingga tidak diisi.
                await connection.ExecuteAsync(
                    @"INSERT INTO purchase_stock
                      (
                          id_supplier,
                          id_produk,
                          id_karyawan,
                          tanggal,
                          jumlah,
                          harga_beli,
                          keterangan
                      )
                      VALUES
                      (
                          @IdSupplier,
                          @IdProduk,
                          NULL,
                          CURDATE(),
                          @Jumlah,
                          @HargaBeli,
                          @Keterangan
                      )",
                    new
                    {
                        model.IdSupplier,
                        model.IdProduk,
                        model.Jumlah,
                        model.HargaBeli,
                        Keterangan = string.IsNullOrWhiteSpace(model.Keterangan)
                            ? "Restock melalui aplikasi Web Syncora"
                            : model.Keterangan.Trim()
                    },
                    transaction);

                // Tambahkan stok tanpa menimpa stok sebelumnya.
                await connection.ExecuteAsync(
                    @"INSERT INTO stock
                      (
                          id_produk,
                          jumlah,
                          stok_minimum
                      )
                      VALUES
                      (
                          @IdProduk,
                          @Jumlah,
                          5
                      )
                      ON DUPLICATE KEY UPDATE
                          jumlah = jumlah + @Jumlah",
                    new
                    {
                        model.IdProduk,
                        model.Jumlah
                    },
                    transaction);

                // Simpan seluruh perubahan secara bersamaan.
                await transaction.CommitAsync();

                TempData["Sukses"] =
                    $"Restock berhasil. Stok bertambah {model.Jumlah} unit.";

                return RedirectToAction(nameof(Riwayat));
            }
            catch
            {
                // Rollback hanya dilakukan jika transaksi masih bisa dibatalkan.
                try
                {
                    await transaction.RollbackAsync();
                }
                catch
                {
                    // Abaikan jika transaksi sudah tidak dapat di-rollback.
                }

                ModelState.AddModelError(
                    "",
                    "Restock gagal disimpan. Periksa data supplier, produk, dan koneksi database.");

                await IsiPilihanRestockAsync(model);

                return View(model);
            }
        }

        // =========================
        // STOK KELUAR
        // =========================

        [HttpGet]
        public async Task<IActionResult> Keluar()
        {
            var model = new TransaksiStokKeluarViewModel();

            await IsiPilihanProdukAsync(model);

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Keluar(
            TransaksiStokKeluarViewModel model)
        {
            if (!ModelState.IsValid)
            {
                await IsiPilihanProdukAsync(model);
                return View(model);
            }

            using var connection = _koneksi.GetConnection();
            await connection.OpenAsync();

            using var transaction = await connection.BeginTransactionAsync();

            try
            {
                // Pastikan produk masih aktif.
                var produkAda = await connection.ExecuteScalarAsync<int>(
                    @"SELECT COUNT(*)
                      FROM produk
                      WHERE id_produk = @IdProduk
                        AND status = 'aktif'",
                    new { model.IdProduk },
                    transaction);

                if (produkAda == 0)
                {
                    await transaction.RollbackAsync();

                    ModelState.AddModelError(
                        "",
                        "Produk tidak ditemukan atau sudah nonaktif.");

                    await IsiPilihanProdukAsync(model);
                    return View(model);
                }

                // Kurangi stok hanya jika jumlahnya mencukupi.
                var affected = await connection.ExecuteAsync(
                    @"UPDATE stock
                      SET jumlah = jumlah - @Jumlah
                      WHERE id_produk = @IdProduk
                        AND jumlah >= @Jumlah",
                    new
                    {
                        model.IdProduk,
                        model.Jumlah
                    },
                    transaction);

                if (affected == 0)
                {
                    await transaction.RollbackAsync();

                    ModelState.AddModelError(
                        "",
                        "Stok tidak mencukupi atau data stok produk belum tersedia.");

                    await IsiPilihanProdukAsync(model);
                    return View(model);
                }

                var idUserText = User.FindFirstValue(
                    ClaimTypes.NameIdentifier);

                int? idUser = int.TryParse(idUserText, out var parsedId)
                    ? parsedId
                    : null;

                // Catat mutasi stok keluar.
                await connection.ExecuteAsync(
                    @"INSERT INTO stock_mutation
                      (
                          id_produk,
                          id_user,
                          tanggal,
                          jenis,
                          jumlah,
                          keterangan
                      )
                      VALUES
                      (
                          @IdProduk,
                          @IdUser,
                          NOW(),
                          'keluar',
                          @Jumlah,
                          @Keterangan
                      )",
                    new
                    {
                        model.IdProduk,
                        IdUser = idUser,
                        model.Jumlah,
                        Keterangan = model.Keterangan.Trim()
                    },
                    transaction);

                await transaction.CommitAsync();

                TempData["Sukses"] =
                    $"Stok keluar berhasil dicatat sebanyak {model.Jumlah} unit.";

                return RedirectToAction(nameof(Riwayat));
            }
            catch
            {
                try
                {
                    await transaction.RollbackAsync();
                }
                catch
                {
                    // Abaikan jika transaksi sudah tidak dapat di-rollback.
                }

                ModelState.AddModelError(
                    "",
                    "Transaksi stok keluar gagal disimpan.");

                await IsiPilihanProdukAsync(model);

                return View(model);
            }
        }

        // =========================
        // RIWAYAT TRANSAKSI
        // =========================

        [HttpGet]
        public async Task<IActionResult> Riwayat(string? cari)
        {
            using var connection = _koneksi.GetConnection();
            await connection.OpenAsync();

            var pola = $"%{cari?.Trim()}%";

            var sql = @"
                SELECT
                    'Masuk' AS JenisTransaksi,
                    ps.tanggal AS Tanggal,
                    p.kode_produk AS KodeProduk,
                    p.nama_produk AS NamaProduk,
                    ps.jumlah AS Jumlah,
                    ps.harga_beli AS HargaBeli,
                    ps.total AS Total,
                    COALESCE(ps.keterangan, 'Restock') AS Keterangan,
                    s.nama_supplier AS NamaSupplier
                FROM purchase_stock ps
                INNER JOIN produk p
                    ON p.id_produk = ps.id_produk
                INNER JOIN supplier s
                    ON s.id_supplier = ps.id_supplier
                WHERE
                    @Cari IS NULL
                    OR p.nama_produk LIKE @Pola
                    OR p.kode_produk LIKE @Pola
                    OR ps.keterangan LIKE @Pola
                    OR s.nama_supplier LIKE @Pola

                UNION ALL

                SELECT
                    'Keluar' AS JenisTransaksi,
                    sm.tanggal AS Tanggal,
                    p.kode_produk AS KodeProduk,
                    p.nama_produk AS NamaProduk,
                    sm.jumlah AS Jumlah,
                    0 AS HargaBeli,
                    0 AS Total,
                    sm.keterangan AS Keterangan,
                    NULL AS NamaSupplier
                FROM stock_mutation sm
                INNER JOIN produk p
                    ON p.id_produk = sm.id_produk
                WHERE
                    @Cari IS NULL
                    OR p.nama_produk LIKE @Pola
                    OR p.kode_produk LIKE @Pola
                    OR sm.keterangan LIKE @Pola

                ORDER BY Tanggal DESC
                LIMIT 300";

            var transaksi = await connection
                .QueryAsync<RiwayatStokItemViewModel>(
                    sql,
                    new
                    {
                        Cari = string.IsNullOrWhiteSpace(cari)
                            ? null
                            : cari.Trim(),
                        Pola = pola
                    });

            var model = new RiwayatStokViewModel
            {
                Transaksi = transaksi.ToList()
            };

            ViewBag.KataKunci = cari;

            return View(model);
        }

        // =========================
        // DATA PILIHAN FORM RESTOCK
        // =========================

        private async Task IsiPilihanRestockAsync(
            TransaksiStokViewModel model)
        {
            using var connection = _koneksi.GetConnection();
            await connection.OpenAsync();

            model.DaftarProduk = (await connection
                .QueryAsync<PilihanStokViewModel>(
                    @"SELECT
                          p.id_produk AS IdProduk,
                          p.nama_produk AS NamaProduk,
                          p.kode_produk AS KodeProduk,
                          COALESCE(s.jumlah, 0) AS JumlahStok
                      FROM produk p
                      LEFT JOIN stock s
                          ON s.id_produk = p.id_produk
                      WHERE p.status = 'aktif'
                      ORDER BY p.nama_produk"))
                .ToList();

            model.DaftarSupplier = (await connection
                .QueryAsync<PilihanSupplierStokViewModel>(
                    @"SELECT
                          id_supplier AS IdSupplier,
                          nama_supplier AS NamaSupplier
                      FROM supplier
                      ORDER BY nama_supplier"))
                .ToList();
        }

        // =========================
        // DATA PILIHAN FORM STOK KELUAR
        // =========================

        private async Task IsiPilihanProdukAsync(
            TransaksiStokKeluarViewModel model)
        {
            using var connection = _koneksi.GetConnection();
            await connection.OpenAsync();

            model.DaftarProduk = (await connection
                .QueryAsync<PilihanStokViewModel>(
                    @"SELECT
                          p.id_produk AS IdProduk,
                          p.nama_produk AS NamaProduk,
                          p.kode_produk AS KodeProduk,
                          COALESCE(s.jumlah, 0) AS JumlahStok
                      FROM produk p
                      INNER JOIN stock s
                          ON s.id_produk = p.id_produk
                      WHERE p.status = 'aktif'
                        AND s.jumlah > 0
                      ORDER BY p.nama_produk"))
                .ToList();
        }
    }
}
