
using System.Security.Claims;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TokoMusik.Web.Data;
using TokoMusik.Web.Models;

namespace TokoMusik.Web.Controllers
{
    [Authorize(Roles = "user")]
    public class AlamatController : Controller
    {
        private readonly Koneksi _koneksi;

        public AlamatController(Koneksi koneksi)
        {
            _koneksi = koneksi;
        }

        private async Task<int?> AmbilIdCustomer(
            MySqlConnector.MySqlConnection connection)
        {
            string? idUser = User.FindFirstValue(
                ClaimTypes.NameIdentifier
            );

            return await connection.QueryFirstOrDefaultAsync<int?>(
                "SELECT id_customer FROM customer WHERE id_user = @IdUser",
                new { IdUser = idUser }
            );
        }

        // GET: /Alamat
        public async Task<IActionResult> Index()
        {
            using var connection = _koneksi.GetConnection();
            await connection.OpenAsync();

            int? idCustomer = await AmbilIdCustomer(connection);

            if (idCustomer == null)
                return RedirectToAction("Index", "User");

            var alamat = await connection.QueryAsync<AlamatCustomer>(
                @"
                SELECT
                    id_alamat AS IdAlamat,
                    id_customer AS IdCustomer,
                    label_alamat AS LabelAlamat,
                    penerima AS Penerima,
                    telepon AS Telepon,
                    alamat_lengkap AS AlamatLengkap,
                    kota AS Kota,
                    provinsi AS Provinsi,
                    kode_pos AS KodePos,
                    is_utama AS IsUtama
                FROM alamat_customer
                WHERE id_customer = @IdCustomer
                ORDER BY is_utama DESC, id_alamat DESC
                ",
                new { IdCustomer = idCustomer }
            );

            return View(alamat);
        }

        // GET: /Alamat/Tambah
        [HttpGet]
        public IActionResult Tambah()
        {
            return View(new AlamatCustomer());
        }

        // POST: /Alamat/Tambah
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Tambah(AlamatCustomer model)
        {
            if (!ModelState.IsValid)
                return View(model);

            using var connection = _koneksi.GetConnection();
            await connection.OpenAsync();

            int? idCustomer = await AmbilIdCustomer(connection);

            if (idCustomer == null)
                return RedirectToAction("Index", "User");

            int jumlahAlamat = await connection.ExecuteScalarAsync<int>(
                @"
                SELECT COUNT(*)
                FROM alamat_customer
                WHERE id_customer = @IdCustomer
                ",
                new { IdCustomer = idCustomer }
            );

            bool jadikanUtama = model.IsUtama || jumlahAlamat == 0;

            using var transaction = await connection.BeginTransactionAsync();

            try
            {
                if (jadikanUtama)
                {
                    await connection.ExecuteAsync(
                        @"
                        UPDATE alamat_customer
                        SET is_utama = 0
                        WHERE id_customer = @IdCustomer
                        ",
                        new { IdCustomer = idCustomer },
                        transaction
                    );
                }

                await connection.ExecuteAsync(
                    @"
                    INSERT INTO alamat_customer
                    (
                        id_customer,
                        label_alamat,
                        penerima,
                        telepon,
                        alamat_lengkap,
                        kota,
                        provinsi,
                        kode_pos,
                        is_utama
                    )
                    VALUES
                    (
                        @IdCustomer,
                        @LabelAlamat,
                        @Penerima,
                        @Telepon,
                        @AlamatLengkap,
                        @Kota,
                        @Provinsi,
                        @KodePos,
                        @IsUtama
                    )
                    ",
                    new
                    {
                        IdCustomer = idCustomer,
                        model.LabelAlamat,
                        model.Penerima,
                        model.Telepon,
                        model.AlamatLengkap,
                        model.Kota,
                        model.Provinsi,
                        model.KodePos,
                        IsUtama = jadikanUtama
                    },
                    transaction
                );

                await transaction.CommitAsync();

                TempData["Success"] = "Alamat berhasil ditambahkan.";
                return RedirectToAction("Index");
            }
            catch
            {
                await transaction.RollbackAsync();

                ModelState.AddModelError(
                    "",
                    "Alamat gagal disimpan. Silakan coba kembali."
                );

                return View(model);
            }
        }

        // POST: /Alamat/JadikanUtama
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> JadikanUtama(int idAlamat)
        {
            using var connection = _koneksi.GetConnection();
            await connection.OpenAsync();

            int? idCustomer = await AmbilIdCustomer(connection);

            if (idCustomer == null)
                return RedirectToAction("Index", "User");

            using var transaction = await connection.BeginTransactionAsync();

            try
            {
                int alamatAda = await connection.ExecuteScalarAsync<int>(
                    @"
                    SELECT COUNT(*)
                    FROM alamat_customer
                    WHERE id_alamat = @IdAlamat
                      AND id_customer = @IdCustomer
                    ",
                    new { IdAlamat = idAlamat, IdCustomer = idCustomer },
                    transaction
                );

                if (alamatAda == 0)
                {
                    await transaction.RollbackAsync();
                    return NotFound();
                }

                await connection.ExecuteAsync(
                    @"
                    UPDATE alamat_customer
                    SET is_utama = 0
                    WHERE id_customer = @IdCustomer
                    ",
                    new { IdCustomer = idCustomer },
                    transaction
                );

                await connection.ExecuteAsync(
                    @"
                    UPDATE alamat_customer
                    SET is_utama = 1
                    WHERE id_alamat = @IdAlamat
                      AND id_customer = @IdCustomer
                    ",
                    new { IdAlamat = idAlamat, IdCustomer = idCustomer },
                    transaction
                );

                await transaction.CommitAsync();

                TempData["Success"] = "Alamat utama berhasil diubah.";
            }
            catch
            {
                await transaction.RollbackAsync();
                TempData["Error"] = "Gagal mengubah alamat utama.";
            }

            return RedirectToAction("Index");
        }

        // POST: /Alamat/Hapus
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Hapus(int idAlamat)
        {
            using var connection = _koneksi.GetConnection();
            await connection.OpenAsync();

            int? idCustomer = await AmbilIdCustomer(connection);

            if (idCustomer == null)
                return RedirectToAction("Index", "User");

            using var transaction = await connection.BeginTransactionAsync();

            try
            {
                var alamat = await connection.QueryFirstOrDefaultAsync<AlamatCustomer>(
                    @"
                    SELECT
                        id_alamat AS IdAlamat,
                        id_customer AS IdCustomer,
                        is_utama AS IsUtama
                    FROM alamat_customer
                    WHERE id_alamat = @IdAlamat
                      AND id_customer = @IdCustomer
                    ",
                    new { IdAlamat = idAlamat, IdCustomer = idCustomer },
                    transaction
                );

                if (alamat == null)
                {
                    await transaction.RollbackAsync();
                    return NotFound();
                }

                int dipakaiPesanan = await connection.ExecuteScalarAsync<int>(
                    @"
                    SELECT COUNT(*)
                    FROM pemesanan
                    WHERE id_alamat = @IdAlamat
                    ",
                    new { IdAlamat = idAlamat },
                    transaction
                );

                if (dipakaiPesanan > 0)
                {
                    await transaction.RollbackAsync();

                    TempData["Error"] =
                        "Alamat tidak dapat dihapus karena sudah digunakan pada riwayat pesanan.";

                    return RedirectToAction("Index");
                }

                await connection.ExecuteAsync(
                    @"
                    DELETE FROM alamat_customer
                    WHERE id_alamat = @IdAlamat
                      AND id_customer = @IdCustomer
                    ",
                    new { IdAlamat = idAlamat, IdCustomer = idCustomer },
                    transaction
                );

                if (alamat.IsUtama)
                {
                    int? alamatBerikutnya =
                        await connection.ExecuteScalarAsync<int?>(
                            @"
                            SELECT id_alamat
                            FROM alamat_customer
                            WHERE id_customer = @IdCustomer
                            ORDER BY id_alamat DESC
                            LIMIT 1
                            ",
                            new { IdCustomer = idCustomer },
                            transaction
                        );

                    if (alamatBerikutnya.HasValue)
                    {
                        await connection.ExecuteAsync(
                            @"
                            UPDATE alamat_customer
                            SET is_utama = 1
                            WHERE id_alamat = @IdAlamat
                              AND id_customer = @IdCustomer
                            ",
                            new
                            {
                                IdAlamat = alamatBerikutnya.Value,
                                IdCustomer = idCustomer
                            },
                            transaction
                        );
                    }
                }

                await transaction.CommitAsync();

                TempData["Success"] = "Alamat berhasil dihapus.";
            }
            catch
            {
                await transaction.RollbackAsync();
                TempData["Error"] = "Gagal menghapus alamat.";
            }

            return RedirectToAction("Index");
        }
    }
}
