
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TokoMusik.Web.Data;
using TokoMusik.Web.Models;

namespace TokoMusik.Web.Controllers
{
    [Authorize(Roles = "admin")]
    public class AdminSupplierController : Controller
    {
        private readonly Koneksi _koneksi;

        public AdminSupplierController(Koneksi koneksi)
        {
            _koneksi = koneksi;
        }

        // ==========================================
        // DAFTAR SUPPLIER
        // ==========================================

        public async Task<IActionResult> Index(string? cari)
        {
            await using var connection = _koneksi.GetConnection();
            await connection.OpenAsync();

            string kondisi = @"
                WHERE (@Cari IS NULL
                    OR s.nama_supplier LIKE CONCAT('%', @Cari, '%')
                    OR s.telepon LIKE CONCAT('%', @Cari, '%')
                    OR s.email LIKE CONCAT('%', @Cari, '%'))";

            string sql = $@"
                SELECT
                    s.id_supplier AS IdSupplier,
                    s.nama_supplier AS NamaSupplier,
                    s.telepon AS Telepon,
                    s.email AS Email,
                    s.alamat AS Alamat,
                    COUNT(ps.id_purchase) AS TotalTransaksi,
                    COALESCE(SUM(ps.total), 0) AS TotalPembelian
                FROM supplier s
                LEFT JOIN purchase_stock ps
                    ON ps.id_supplier = s.id_supplier
                {kondisi}
                GROUP BY
                    s.id_supplier,
                    s.nama_supplier,
                    s.telepon,
                    s.email,
                    s.alamat
                ORDER BY s.nama_supplier ASC;";

            var daftar = (await connection.QueryAsync<SupplierViewModel>(sql, new
            {
                Cari = string.IsNullOrWhiteSpace(cari) ? null : cari.Trim()
            })).ToList();

            string sqlRingkasan = @"
                SELECT
                    (SELECT COUNT(*) FROM supplier) AS TotalSupplier,
                    COUNT(ps.id_purchase) AS TotalTransaksiPembelian,
                    COALESCE(SUM(ps.total), 0) AS TotalNilaiPembelian
                FROM purchase_stock ps;";

            var ringkasan = await connection.QuerySingleAsync<dynamic>(sqlRingkasan);

            var model = new SupplierIndexViewModel
            {
                KataKunci = cari ?? string.Empty,
                TotalSupplier = (int)ringkasan.TotalSupplier,
                TotalTransaksiPembelian = (int)ringkasan.TotalTransaksiPembelian,
                TotalNilaiPembelian = (decimal)ringkasan.TotalNilaiPembelian,
                DaftarSupplier = daftar
            };

            return View(model);
        }

        // ==========================================
        // TAMBAH SUPPLIER - GET
        // ==========================================

        [HttpGet]
        public IActionResult Tambah()
        {
            return View(new SupplierViewModel());
        }

        // ==========================================
        // TAMBAH SUPPLIER - POST
        // ==========================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Tambah(SupplierViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            await using var connection = _koneksi.GetConnection();
            await connection.OpenAsync();

            int namaDuplikat = await connection.ExecuteScalarAsync<int>(@"
                SELECT COUNT(*)
                FROM supplier
                WHERE LOWER(nama_supplier) = LOWER(@NamaSupplier);",
                new { model.NamaSupplier });

            if (namaDuplikat > 0)
            {
                ModelState.AddModelError(
                    nameof(model.NamaSupplier),
                    "Nama supplier tersebut sudah terdaftar."
                );

                return View(model);
            }

            const string sql = @"
                INSERT INTO supplier
                    (nama_supplier, telepon, email, alamat)
                VALUES
                    (@NamaSupplier, @Telepon, @Email, @Alamat);";

            await connection.ExecuteAsync(sql, new
            {
                NamaSupplier = model.NamaSupplier.Trim(),
                Telepon = string.IsNullOrWhiteSpace(model.Telepon)
                    ? null : model.Telepon.Trim(),
                Email = string.IsNullOrWhiteSpace(model.Email)
                    ? null : model.Email.Trim(),
                Alamat = string.IsNullOrWhiteSpace(model.Alamat)
                    ? null : model.Alamat.Trim()
            });

            TempData["Success"] = "Supplier berhasil ditambahkan.";

            return RedirectToAction(nameof(Index));
        }

        // ==========================================
        // EDIT SUPPLIER - GET
        // ==========================================

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            await using var connection = _koneksi.GetConnection();
            await connection.OpenAsync();

            const string sql = @"
                SELECT
                    id_supplier AS IdSupplier,
                    nama_supplier AS NamaSupplier,
                    telepon AS Telepon,
                    email AS Email,
                    alamat AS Alamat
                FROM supplier
                WHERE id_supplier = @Id;";

            var supplier = await connection.QuerySingleOrDefaultAsync<SupplierViewModel>(
                sql,
                new { Id = id }
            );

            if (supplier == null)
            {
                TempData["Error"] = "Supplier tidak ditemukan.";
                return RedirectToAction(nameof(Index));
            }

            return View(supplier);
        }

        // ==========================================
        // EDIT SUPPLIER - POST
        // ==========================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(SupplierViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            await using var connection = _koneksi.GetConnection();
            await connection.OpenAsync();

            int supplierAda = await connection.ExecuteScalarAsync<int>(@"
                SELECT COUNT(*)
                FROM supplier
                WHERE id_supplier = @IdSupplier;",
                new { model.IdSupplier });

            if (supplierAda == 0)
            {
                TempData["Error"] = "Supplier tidak ditemukan.";
                return RedirectToAction(nameof(Index));
            }

            int namaDuplikat = await connection.ExecuteScalarAsync<int>(@"
                SELECT COUNT(*)
                FROM supplier
                WHERE LOWER(nama_supplier) = LOWER(@NamaSupplier)
                  AND id_supplier <> @IdSupplier;",
                new
                {
                    model.NamaSupplier,
                    model.IdSupplier
                });

            if (namaDuplikat > 0)
            {
                ModelState.AddModelError(
                    nameof(model.NamaSupplier),
                    "Nama supplier tersebut sudah digunakan."
                );

                return View(model);
            }

            const string sql = @"
                UPDATE supplier
                SET
                    nama_supplier = @NamaSupplier,
                    telepon = @Telepon,
                    email = @Email,
                    alamat = @Alamat
                WHERE id_supplier = @IdSupplier;";

            await connection.ExecuteAsync(sql, new
            {
                NamaSupplier = model.NamaSupplier.Trim(),
                Telepon = string.IsNullOrWhiteSpace(model.Telepon)
                    ? null : model.Telepon.Trim(),
                Email = string.IsNullOrWhiteSpace(model.Email)
                    ? null : model.Email.Trim(),
                Alamat = string.IsNullOrWhiteSpace(model.Alamat)
                    ? null : model.Alamat.Trim(),
                model.IdSupplier
            });

            TempData["Success"] = "Data supplier berhasil diperbarui.";

            return RedirectToAction(nameof(Index));
        }

        // ==========================================
        // DETAIL SUPPLIER DAN RIWAYAT PEMBELIAN
        // ==========================================

        [HttpGet]
        public async Task<IActionResult> Detail(int id)
        {
            await using var connection = _koneksi.GetConnection();
            await connection.OpenAsync();

            const string sqlSupplier = @"
                SELECT
                    id_supplier AS IdSupplier,
                    nama_supplier AS NamaSupplier,
                    telepon AS Telepon,
                    email AS Email,
                    alamat AS Alamat
                FROM supplier
                WHERE id_supplier = @Id;";

            var supplier = await connection.QuerySingleOrDefaultAsync<SupplierViewModel>(
                sqlSupplier,
                new { Id = id }
            );

            if (supplier == null)
            {
                TempData["Error"] = "Supplier tidak ditemukan.";
                return RedirectToAction(nameof(Index));
            }

            const string sqlRingkasan = @"
                SELECT
                    COUNT(*) AS TotalTransaksi,
                    COALESCE(SUM(jumlah), 0) AS TotalProdukDibeli,
                    COALESCE(SUM(total), 0) AS TotalPembelian
                FROM purchase_stock
                WHERE id_supplier = @IdSupplier;";

            var ringkasan = await connection.QuerySingleAsync<dynamic>(
                sqlRingkasan,
                new { IdSupplier = id }
            );

            const string sqlRiwayat = @"
                SELECT
                    ps.id_purchase AS IdPurchase,
                    ps.tanggal AS Tanggal,
                    p.nama_produk AS NamaProduk,
                    p.kode_produk AS KodeProduk,
                    ps.jumlah AS Jumlah,
                    ps.harga_beli AS HargaBeli,
                    ps.total AS Total,
                    ps.keterangan AS Keterangan
                FROM purchase_stock ps
                INNER JOIN produk p
                    ON p.id_produk = ps.id_produk
                WHERE ps.id_supplier = @IdSupplier
                ORDER BY ps.tanggal DESC, ps.id_purchase DESC;";

            var riwayat = (await connection.QueryAsync<RiwayatPembelianSupplierViewModel>(
                sqlRiwayat,
                new { IdSupplier = id }
            )).ToList();

            var model = new SupplierDetailViewModel
            {
                Supplier = supplier,
                TotalTransaksi = (int)ringkasan.TotalTransaksi,
                TotalProdukDibeli = (int)ringkasan.TotalProdukDibeli,
                TotalPembelian = (decimal)ringkasan.TotalPembelian,
                RiwayatPembelian = riwayat
            };

            return View(model);
        }

        // ==========================================
        // HAPUS SUPPLIER
        // Hanya bisa jika belum punya transaksi
        // ==========================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Hapus(int id)
        {
            await using var connection = _koneksi.GetConnection();
            await connection.OpenAsync();

            int jumlahTransaksi = await connection.ExecuteScalarAsync<int>(@"
                SELECT COUNT(*)
                FROM purchase_stock
                WHERE id_supplier = @Id;",
                new { Id = id });

            if (jumlahTransaksi > 0)
            {
                TempData["Error"] =
                    "Supplier tidak dapat dihapus karena sudah memiliki riwayat pembelian.";

                return RedirectToAction(nameof(Index));
            }

            int supplierAda = await connection.ExecuteScalarAsync<int>(@"
                SELECT COUNT(*)
                FROM supplier
                WHERE id_supplier = @Id;",
                new { Id = id });

            if (supplierAda == 0)
            {
                TempData["Error"] = "Supplier tidak ditemukan.";
                return RedirectToAction(nameof(Index));
            }

            await connection.ExecuteAsync(@"
                DELETE FROM supplier
                WHERE id_supplier = @Id;",
                new { Id = id });

            TempData["Success"] = "Supplier berhasil dihapus.";

            return RedirectToAction(nameof(Index));
        }
    }
}
