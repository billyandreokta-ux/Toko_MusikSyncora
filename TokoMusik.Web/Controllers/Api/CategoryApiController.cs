
using Dapper;
using Microsoft.AspNetCore.Mvc;
using MySqlConnector;
using TokoMusik.Web.Data;
using TokoMusik.Web.Models;

namespace TokoMusik.Web.Controllers.Api;

[ApiController]
[Route("api/category")]
public class CategoryApiController : ControllerBase
{
    private readonly Koneksi _koneksi;

    public CategoryApiController(Koneksi koneksi)
    {
        _koneksi = koneksi;
    }

    private const string Query = """
        SELECT
            id_category AS IdCategory,
            nama_category AS NamaCategory,
            deskripsi AS Deskripsi
        FROM category
        """;

    [HttpGet]
    public async Task<IActionResult> GetSemua()
    {
        using var db = _koneksi.GetConnection();

        var data = await db.QueryAsync<Category>(
            Query + " ORDER BY nama_category"
        );

        return Ok(data);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        using var db = _koneksi.GetConnection();

        var data = await db.QueryFirstOrDefaultAsync<Category>(
            Query + " WHERE id_category = @id",
            new { id }
        );

        return data == null
            ? NotFound(new { message = "Kategori tidak ditemukan." })
            : Ok(data);
    }

    [HttpPost]
    public async Task<IActionResult> Tambah([FromBody] Category model)
    {
        using var db = _koneksi.GetConnection();

        try
        {
            const string sql = """
                INSERT INTO category (nama_category, deskripsi)
                VALUES (@NamaCategory, @Deskripsi);
                """;

            await db.ExecuteAsync(sql, model);

            var id = await db.ExecuteScalarAsync<int>(
                "SELECT LAST_INSERT_ID();"
            );

            model.IdCategory = id;

            return CreatedAtAction(
                nameof(GetById),
                new { id },
                model
            );
        }
        catch (MySqlException ex) when (ex.Number == 1062)
        {
            return Conflict(new
            {
                message = "Nama kategori sudah digunakan."
            });
        }
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Ubah(
        int id,
        [FromBody] Category model)
    {
        using var db = _koneksi.GetConnection();

        try
        {
            const string sql = """
                UPDATE category
                SET nama_category = @NamaCategory,
                    deskripsi = @Deskripsi
                WHERE id_category = @Id;
                """;

            int affected = await db.ExecuteAsync(
                sql,
                new
                {
                    model.NamaCategory,
                    model.Deskripsi,
                    Id = id
                }
            );

            return affected == 0
                ? NotFound(new { message = "Kategori tidak ditemukan." })
                : Ok(new { message = "Kategori berhasil diperbarui." });
        }
        catch (MySqlException ex) when (ex.Number == 1062)
        {
            return Conflict(new
            {
                message = "Nama kategori sudah digunakan."
            });
        }
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Hapus(int id)
    {
        using var db = _koneksi.GetConnection();

        try
        {
            int affected = await db.ExecuteAsync(
                "DELETE FROM category WHERE id_category = @id",
                new { id }
            );

            return affected == 0
                ? NotFound(new { message = "Kategori tidak ditemukan." })
                : Ok(new { message = "Kategori berhasil dihapus." });
        }
        catch (MySqlException ex) when (ex.Number == 1451)
        {
            return Conflict(new
            {
                message = "Kategori masih digunakan oleh produk."
            });
        }
    }
}
