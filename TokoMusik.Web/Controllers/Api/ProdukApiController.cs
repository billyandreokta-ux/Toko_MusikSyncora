
using Dapper;
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
        [HttpGet]
        public async Task<IActionResult> GetSemua()
        {
            using var db = _koneksi.GetConnection();

            var data = await db.QueryAsync<Produk>(QueryProduk);

            return Ok(data);
        }

        // GET: api/produk/1
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            using var db = _koneksi.GetConnection();

            var data = await db.QueryFirstOrDefaultAsync<Produk>(
                QueryProduk + " WHERE id_produk = @id",
                new { id }
            );

            if (data == null)
            {
                return NotFound(new
                {
                    message = "Produk tidak ditemukan."
                });
            }

            return Ok(data);
        }

        // POST: api/produk
        [HttpPost]
        public async Task<IActionResult> Tambah(
            [FromBody] Produk produk)
        {
            using var db = _koneksi.GetConnection();

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

                var id = await db.ExecuteScalarAsync<long>(
                    sql, produk
                );

                produk.IdProduk = (int)id;

                return Created(
                    $"/api/produk/{id}",
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
                    message = "Kategori atau merk tidak ditemukan."
                });
            }
        }

        // PUT: api/produk/1
        [HttpPut("{id:int}")]
        public async Task<IActionResult> Ubah(
            int id,
            [FromBody] Produk produk)
        {
            using var db = _koneksi.GetConnection();

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
                    WHERE id_produk = @IdProduk";

                produk.IdProduk = id;

                var jumlah = await db.ExecuteAsync(
                    sql, produk
                );

                if (jumlah == 0)
                {
                    return NotFound(new
                    {
                        message = "Produk tidak ditemukan."
                    });
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
                    message = "Kategori atau merk tidak ditemukan."
                });
            }
        }

        // DELETE: api/produk/1
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Hapus(int id)
        {
            using var db = _koneksi.GetConnection();

            try
            {
                var jumlah = await db.ExecuteAsync(
                    "DELETE FROM produk WHERE id_produk = @id",
                    new { id }
                );

                if (jumlah == 0)
                {
                    return NotFound(new
                    {
                        message = "Produk tidak ditemukan."
                    });
                }

                return Ok(new
                {
                    message = "Produk berhasil dihapus."
                });
            }
            catch (MySqlException ex) when (ex.Number == 1451)
            {
                return Conflict(new
                {
                    message = "Produk sudah digunakan dalam transaksi. Ubah status menjadi nonaktif."
                });
            }
        }
    }
}
