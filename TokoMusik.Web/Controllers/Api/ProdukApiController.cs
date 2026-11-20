
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MySqlConnector;
using TokoMusik.Web.Data;
using TokoMusik.Web.Models;

namespace TokoMusik.Web.Controllers.Api
{
    [ApiController]
    [Route("api/produk")]
    public class ProdukApiController : ControllerBase
    {
        private readonly Koneksi _koneksi;

        public ProdukApiController(Koneksi koneksi)
        {
            _koneksi = koneksi;
        }

        private const string QueryProduk = @"
            SELECT
                id_produk AS IdProduk,
                id_category AS IdCategory,
                id_merk AS IdMerk,
                kode_produk AS KodeProduk,
                nama_produk AS NamaProduk,
                harga_jual AS HargaJual,
                deskripsi AS Deskripsi,
                foto AS Foto,
                status AS Status
            FROM produk";

        // GET: api/produk
        // Pelanggan hanya mendapatkan produk aktif.
        [AllowAnonymous]
        [HttpGet]
        public async Task<IActionResult> GetSemua()
        {
            using var db = _koneksi.GetConnection();
            await db.OpenAsync();

            var data = await db.QueryAsync<Produk>(
                QueryProduk + " WHERE status = 'aktif' ORDER BY nama_produk ASC;"
            );

            return Ok(data);
        }

        // GET: api/produk/1
        [AllowAnonymous]
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            if (id <= 0)
                return BadRequest(new { message = "ID produk tidak valid." });

            using var db = _koneksi.GetConnection();
            await db.OpenAsync();

            var data = await db.QueryFirstOrDefaultAsync<Produk>(
                QueryProduk + @"
                    WHERE id_produk = @Id
                      AND status = 'aktif'
                    LIMIT 1;",
                new { Id = id }
            );

            if (data == null)
            {
                return NotFound(new
                {
                    message = "Produk tidak ditemukan atau sudah tidak aktif."
                });
            }

            return Ok(data);
        }

        // POST: api/produk
        // Hanya Admin yang boleh menambahkan produk.
        [Authorize(Roles = "admin")]
        [HttpPost]
        public async Task<IActionResult> Tambah([FromBody] Produk produk)
        {
            if (!ModelState.IsValid)
                return ValidationProblem(ModelState);

            if (produk.IdCategory <= 0 || produk.IdMerk <= 0)
            {
                return BadRequest(new
                {
                    message = "Kategori dan merek wajib dipilih."
                });
            }

            if (produk.HargaJual <= 0)
            {
                return BadRequest(new
                {
                    message = "Harga produk harus lebih dari nol."
                });
            }

            produk.KodeProduk = produk.KodeProduk.Trim();
            produk.NamaProduk = produk.NamaProduk.Trim();
            produk.Status = string.IsNullOrWhiteSpace(produk.Status)
                ? "aktif"
                : produk.Status.Trim().ToLowerInvariant();

            if (produk.Status != "aktif" && produk.Status != "nonaktif")
            {
                return BadRequest(new
                {
                    message = "Status produk harus aktif atau nonaktif."
                });
            }

            using var db = _koneksi.GetConnection();
            await db.OpenAsync();

            try
            {
                const string sql = @"
                    INSERT INTO produk
                    (
                        id_category,
                        id_merk,
                        kode_produk,
                        nama_produk,
                        harga_jual,
                        deskripsi,
                        foto,
                        status
                    )
                    VALUES
                    (
                        @IdCategory,
                        @IdMerk,
                        @KodeProduk,
                        @NamaProduk,
                        @HargaJual,
                        @Deskripsi,
                        @Foto,
                        @Status
                    );

                    SELECT LAST_INSERT_ID();";

                var id = await db.ExecuteScalarAsync<long>(sql, produk);

                produk.IdProduk = (int)id;

                return CreatedAtAction(
                    nameof(GetById),
                    new { id = produk.IdProduk },
                    new
                    {
                        message = "Produk berhasil ditambahkan.",
                        data = produk
                    }
                );
            }
            catch (MySqlException ex) when (ex.Number == 1062)
            {
                return Conflict(new
                {
                    message = "Kode produk sudah digunakan."
                });
            }
            catch (MySqlException ex) when (ex.Number == 1452)
            {
                return BadRequest(new
                {
                    message = "Kategori atau merek tidak ditemukan."
                });
            }
        }

        // PUT: api/produk/1
        // Hanya Admin yang boleh mengubah produk.
        [Authorize(Roles = "admin")]
        [HttpPut("{id:int}")]
        public async Task<IActionResult> Ubah(
            int id,
            [FromBody] Produk produk)
        {
            if (id <= 0)
                return BadRequest(new { message = "ID produk tidak valid." });

            if (!ModelState.IsValid)
                return ValidationProblem(ModelState);

            if (produk.IdCategory <= 0 || produk.IdMerk <= 0)
            {
                return BadRequest(new
                {
                    message = "Kategori dan merek wajib dipilih."
                });
            }

            if (produk.HargaJual <= 0)
            {
                return BadRequest(new
                {
                    message = "Harga produk harus lebih dari nol."
                });
            }

            produk.KodeProduk = produk.KodeProduk.Trim();
            produk.NamaProduk = produk.NamaProduk.Trim();
            produk.Status = string.IsNullOrWhiteSpace(produk.Status)
                ? "aktif"
                : produk.Status.Trim().ToLowerInvariant();

            if (produk.Status != "aktif" && produk.Status != "nonaktif")
            {
                return BadRequest(new
                {
                    message = "Status produk harus aktif atau nonaktif."
                });
            }

            produk.IdProduk = id;

            using var db = _koneksi.GetConnection();
            await db.OpenAsync();

            try
            {
                const string sql = @"
                    UPDATE produk SET
                        id_category = @IdCategory,
                        id_merk = @IdMerk,
                        kode_produk = @KodeProduk,
                        nama_produk = @NamaProduk,
                        harga_jual = @HargaJual,
                        deskripsi = @Deskripsi,
                        foto = @Foto,
                        status = @Status
                    WHERE id_produk = @IdProduk;";

                var jumlah = await db.ExecuteAsync(sql, produk);

                if (jumlah == 0)
                {
                    var ada = await db.ExecuteScalarAsync<int>(
                        "SELECT COUNT(*) FROM produk WHERE id_produk = @Id;",
                        new { Id = id }
                    );

                    if (ada == 0)
                    {
                        return NotFound(new
                        {
                            message = "Produk tidak ditemukan."
                        });
                    }
                }

                return Ok(new
                {
                    message = "Produk berhasil diperbarui."
                });
            }
            catch (MySqlException ex) when (ex.Number == 1062)
            {
                return Conflict(new
                {
                    message = "Kode produk sudah digunakan."
                });
            }
            catch (MySqlException ex) when (ex.Number == 1452)
            {
                return BadRequest(new
                {
                    message = "Kategori atau merek tidak ditemukan."
                });
            }
        }

        // DELETE: api/produk/1
        // Menggunakan soft delete agar riwayat transaksi tetap aman.
        [Authorize(Roles = "admin")]
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Hapus(int id)
        {
            if (id <= 0)
                return BadRequest(new { message = "ID produk tidak valid." });

            using var db = _koneksi.GetConnection();
            await db.OpenAsync();

            const string sql = @"
                UPDATE produk
                SET status = 'nonaktif'
                WHERE id_produk = @Id
                  AND status <> 'nonaktif';";

            var jumlah = await db.ExecuteAsync(sql, new { Id = id });

            if (jumlah == 0)
            {
                var ada = await db.ExecuteScalarAsync<int>(
                    "SELECT COUNT(*) FROM produk WHERE id_produk = @Id;",
                    new { Id = id }
                );

                if (ada == 0)
                {
                    return NotFound(new
                    {
                        message = "Produk tidak ditemukan."
                    });
                }

                return Ok(new
                {
                    message = "Produk sudah berstatus nonaktif."
                });
            }

            return Ok(new
            {
                message = "Produk berhasil dinonaktifkan."
            });
        }
    }
}
