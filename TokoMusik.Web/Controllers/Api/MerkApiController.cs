
using Dapper;
using Microsoft.AspNetCore.Mvc;
using MySqlConnector;
using TokoMusik.Web.Data;
using TokoMusik.Web.Models;

namespace TokoMusik.Web.Controllers.Api;

[ApiController]
[Route("api/merk")]
public class MerkApiController : ControllerBase
{
    private readonly Koneksi _koneksi;

    public MerkApiController(Koneksi koneksi)
    {
        _koneksi = koneksi;
    }

    private const string Query = """
        SELECT
            id_merk AS IdMerk,
            nama_merk AS NamaMerk,
            negara AS Negara
        FROM merk
        """;

    [HttpGet]
    public async Task<IActionResult> GetSemua()
    {
        using var db = _koneksi.GetConnection();

        var data = await db.QueryAsync<Merk>(
            Query + " ORDER BY nama_merk"
        );

        return Ok(data);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        using var db = _koneksi.GetConnection();

        var data = await db.QueryFirstOrDefaultAsync<Merk>(
            Query + " WHERE id_merk = @id",
            new { id }
        );

        return data == null
            ? NotFound(new { message = "Merek tidak ditemukan." })
            : Ok(data);
    }

    [HttpPost]
    public async Task<IActionResult> Tambah([FromBody] Merk model)
    {
        using var db = _koneksi.GetConnection();

        try
        {
            const string sql = """
                INSERT INTO merk (nama_merk, negara)
                VALUES (@NamaMerk, @Negara);
                """;

            await db.ExecuteAsync(sql, model);

            int id = await db.ExecuteScalarAsync<int>(
                "SELECT LAST_INSERT_ID();"
            );

            model.IdMerk = id;

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
                message = "Nama merek sudah terdaftar."
            });
        }
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Ubah(
        int id,
        [FromBody] Merk model)
    {
        using var db = _koneksi.GetConnection();

        try
        {
            const string sql = """
                UPDATE merk
                SET nama_merk = @NamaMerk,
                    negara = @Negara
                WHERE id_merk = @Id;
                """;

            int affected = await db.ExecuteAsync(
                sql,
                new
                {
                    model.NamaMerk,
                    model.Negara,
                    Id = id
                }
            );

            return affected == 0
                ? NotFound(new { message = "Merek tidak ditemukan." })
                : Ok(new { message = "Merek berhasil diperbarui." });
        }
        catch (MySqlException ex) when (ex.Number == 1062)
        {
            return Conflict(new
            {
                message = "Nama merek sudah terdaftar."
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
                "DELETE FROM merk WHERE id_merk = @id",
                new { id }
            );

            return affected == 0
                ? NotFound(new { message = "Merek tidak ditemukan." })
                : Ok(new { message = "Merek berhasil dihapus." });
        }
        catch (MySqlException ex) when (ex.Number == 1451)
        {
            return Conflict(new
            {
                message = "Merek masih digunakan oleh produk."
            });
        }
    }
}
