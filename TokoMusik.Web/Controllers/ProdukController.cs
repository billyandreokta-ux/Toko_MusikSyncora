
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TokoMusik.Web.Data;

namespace TokoMusik.Web.Controllers
{
    [AllowAnonymous]
    public class ProdukController : Controller
    {
        private readonly Koneksi _koneksi;

        public ProdukController(Koneksi koneksi)
        {
            _koneksi = koneksi;
        }

        // GET: /Produk
        public async Task<IActionResult> Index(string? cari, int? kategori)
        {
            using var connection = _koneksi.GetConnection();
            await connection.OpenAsync();

            const string sqlDasar = @"
                SELECT
                    p.id_produk AS IdProduk,
                    p.id_category AS IdCategory,
                    p.id_merk AS IdMerk,
                    p.kode_produk AS KodeProduk,
                    p.nama_produk AS NamaProduk,
                    p.harga_jual AS HargaJual,
                    p.deskripsi AS Deskripsi,
                    p.foto AS Foto,
                    p.status AS Status,
                    c.nama_category AS NamaCategory,
                    m.nama_merk AS NamaMerk,
                    COALESCE(
                        (
                            SELECT SUM(s.jumlah)
                            FROM stock s
                            WHERE s.id_produk = p.id_produk
                        ), 0
                    ) AS JumlahStok
                FROM produk p
                LEFT JOIN category c
                    ON p.id_category = c.id_category
                LEFT JOIN merk m
                    ON p.id_merk = m.id_merk
                WHERE p.status = 'aktif'
            ";

            var sql = sqlDasar;
            var parameters = new DynamicParameters();

            if (!string.IsNullOrWhiteSpace(cari))
            {
                sql += @"
                    AND (
                        p.nama_produk LIKE @Cari
                        OR p.kode_produk LIKE @Cari
                        OR m.nama_merk LIKE @Cari
                        OR c.nama_category LIKE @Cari
                    )
                ";

                parameters.Add("Cari", $"%{cari.Trim()}%");
            }

            if (kategori.HasValue && kategori.Value > 0)
            {
                sql += " AND p.id_category = @Kategori ";
                parameters.Add("Kategori", kategori.Value);
            }

            sql += " ORDER BY p.nama_produk ASC;";

            var produk = await connection.QueryAsync(sql, parameters);

            var daftarKategori = await connection.QueryAsync(@"
                SELECT
                    id_category AS IdCategory,
                    nama_category AS NamaCategory
                FROM category
                ORDER BY nama_category;
            ");

            ViewBag.Kategori = daftarKategori;
            ViewBag.Cari = cari;
            ViewBag.KategoriTerpilih = kategori;

            return View(produk);
        }

        // GET: /Produk/Detail/1
        public async Task<IActionResult> Detail(int id)
        {
            if (id <= 0)
                return NotFound();

            using var connection = _koneksi.GetConnection();
            await connection.OpenAsync();

            const string sql = @"
                SELECT
                    p.id_produk AS IdProduk,
                    p.id_category AS IdCategory,
                    p.id_merk AS IdMerk,
                    p.kode_produk AS KodeProduk,
                    p.nama_produk AS NamaProduk,
                    p.harga_jual AS HargaJual,
                    p.deskripsi AS Deskripsi,
                    p.foto AS Foto,
                    p.status AS Status,
                    c.nama_category AS NamaCategory,
                    m.nama_merk AS NamaMerk,
                    COALESCE(
                        (
                            SELECT SUM(s.jumlah)
                            FROM stock s
                            WHERE s.id_produk = p.id_produk
                        ), 0
                    ) AS JumlahStok,
                    d.warna AS Warna,
                    d.bahan AS Bahan,
                    d.ukuran AS Ukuran,
                    d.berat_gram AS BeratGram,
                    d.spesifikasi AS Spesifikasi
                FROM produk p
                LEFT JOIN category c
                    ON p.id_category = c.id_category
                LEFT JOIN merk m
                    ON p.id_merk = m.id_merk
                LEFT JOIN detail_produk d
                    ON p.id_produk = d.id_produk
                WHERE p.id_produk = @Id
                  AND p.status = 'aktif'
                LIMIT 1;
            ";

            var produk = await connection.QueryFirstOrDefaultAsync(
                sql,
                new { Id = id }
            );

            if (produk == null)
                return NotFound();

            return View(produk);
        }
    }
}
