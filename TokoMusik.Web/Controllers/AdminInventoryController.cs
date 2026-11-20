using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TokoMusik.Web.Data;
using TokoMusik.Web.Models;

namespace TokoMusik.Web.Controllers
{
    [Authorize(Roles = "admin")]
    public class AdminInventoryController : Controller
    {
        private readonly Koneksi _koneksi;

        public AdminInventoryController(Koneksi koneksi)
        {
            _koneksi = koneksi;
        }

        public async Task<IActionResult> Index(
            string? cari,
            string? status = "semua")
        {
            using var connection = _koneksi.GetConnection();
            await connection.OpenAsync();

            cari = string.IsNullOrWhiteSpace(cari)
                ? null
                : cari.Trim();

            status = string.IsNullOrWhiteSpace(status)
                ? "semua"
                : status.Trim().ToLowerInvariant();

            if (status is not ("semua" or "aman" or "menipis" or "habis"))
                status = "semua";

            var sql = @"
                SELECT
                    p.id_produk AS IdProduk,
                    p.kode_produk AS KodeProduk,
                    p.nama_produk AS NamaProduk,
                    COALESCE(c.nama_category, '-') AS NamaCategory,
                    COALESCE(m.nama_merk, '-') AS NamaMerk,
                    p.harga_jual AS HargaJual,
                    p.foto AS Foto,
                    p.status AS StatusProduk,
                    COALESCE(s.jumlah, 0) AS JumlahStok,
                    COALESCE(s.stok_minimum, 5) AS StokMinimum
                FROM produk p
                LEFT JOIN category c
                    ON p.id_category = c.id_category
                LEFT JOIN merk m
                    ON p.id_merk = m.id_merk
                LEFT JOIN stock s
                    ON p.id_produk = s.id_produk
                WHERE
                    (
                        @Cari IS NULL
                        OR p.nama_produk LIKE @Pola
                        OR p.kode_produk LIKE @Pola
                        OR m.nama_merk LIKE @Pola
                        OR c.nama_category LIKE @Pola
                    )
                    AND
                    (
                        @Status = 'semua'
                        OR (@Status = 'habis'
                            AND COALESCE(s.jumlah, 0) <= 0)
                        OR (@Status = 'menipis'
                            AND COALESCE(s.jumlah, 0) > 0
                            AND COALESCE(s.jumlah, 0)
                                <= COALESCE(s.stok_minimum, 5))
                        OR (@Status = 'aman'
                            AND COALESCE(s.jumlah, 0)
                                > COALESCE(s.stok_minimum, 5))
                    )
                ORDER BY
                    CASE
                        WHEN COALESCE(s.jumlah, 0) <= 0 THEN 1
                        WHEN COALESCE(s.jumlah, 0)
                            <= COALESCE(s.stok_minimum, 5) THEN 2
                        ELSE 3
                    END,
                    p.nama_produk ASC";

            var daftarProduk = (await connection
                .QueryAsync<InventoryItemViewModel>(
                    sql,
                    new
                    {
                        Cari = cari,
                        Pola = $"%{cari}%",
                        Status = status
                    })).ToList();

            var ringkasan = await connection.QuerySingleAsync<dynamic>(@"
                SELECT
                    COUNT(*) AS TotalProduk,
                    COALESCE(SUM(COALESCE(s.jumlah, 0)), 0) AS TotalStok,
                    COALESCE(SUM(
                        CASE
                            WHEN COALESCE(s.jumlah, 0) <= 0
                            THEN 1 ELSE 0
                        END
                    ), 0) AS StokHabis,
                    COALESCE(SUM(
                        CASE
                            WHEN COALESCE(s.jumlah, 0) > 0
                             AND COALESCE(s.jumlah, 0)
                                 <= COALESCE(s.stok_minimum, 5)
                            THEN 1 ELSE 0
                        END
                    ), 0) AS StokMenipis
                FROM produk p
                LEFT JOIN stock s
                    ON p.id_produk = s.id_produk
                WHERE p.status = 'aktif'");

            var model = new InventoryViewModel
            {
                KataKunci = cari,
                StatusFilter = status,
                Produk = daftarProduk,
                TotalProduk = (int)ringkasan.TotalProduk,
                TotalStok = (int)ringkasan.TotalStok,
                StokHabis = (int)ringkasan.StokHabis,
                StokMenipis = (int)ringkasan.StokMenipis
            };

            return View(model);
        }

        public async Task<IActionResult> Detail(int id)
        {
            if (id <= 0)
                return BadRequest();

            using var connection = _koneksi.GetConnection();
            await connection.OpenAsync();

            var sql = @"
                SELECT
                    p.id_produk AS IdProduk,
                    p.kode_produk AS KodeProduk,
                    p.nama_produk AS NamaProduk,
                    COALESCE(c.nama_category, '-') AS NamaCategory,
                    COALESCE(m.nama_merk, '-') AS NamaMerk,
                    p.harga_jual AS HargaJual,
                    p.deskripsi AS Deskripsi,
                    p.foto AS Foto,
                    p.status AS StatusProduk,
                    COALESCE(s.jumlah, 0) AS JumlahStok,
                    COALESCE(s.stok_minimum, 5) AS StokMinimum,
                    s.updated_at AS UpdatedAt
                FROM produk p
                LEFT JOIN category c
                    ON p.id_category = c.id_category
                LEFT JOIN merk m
                    ON p.id_merk = m.id_merk
                LEFT JOIN stock s
                    ON p.id_produk = s.id_produk
                WHERE p.id_produk = @Id
                LIMIT 1";

            var model = await connection
                .QuerySingleOrDefaultAsync<InventoryDetailViewModel>(
                    sql,
                    new { Id = id });

            if (model == null)
                return NotFound();

            return View(model);
        }
    }
}