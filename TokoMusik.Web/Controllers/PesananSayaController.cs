
using System.Security.Claims;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MySqlConnector;
using TokoMusik.Web.Data;
using TokoMusik.Web.Models;

namespace TokoMusik.Web.Controllers
{
    [Authorize(Roles = "user")]
    [Route("PesananSaya")]
    public class PesananSayaController : Controller
    {
        private readonly Koneksi _koneksi;
        private readonly IWebHostEnvironment _environment;

        private const long MaksimalUkuranFile = 5 * 1024 * 1024;

        public PesananSayaController(
            Koneksi koneksi,
            IWebHostEnvironment environment)
        {
            _koneksi = koneksi;
            _environment = environment;
        }

        private async Task<int?> GetIdCustomerAsync(MySqlConnection connection)
        {
            string? idUser = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (!int.TryParse(idUser, out int idUserInt))
                return null;

            return await connection.QueryFirstOrDefaultAsync<int?>(
                @"SELECT id_customer
                  FROM customer
                  WHERE id_user = @IdUser",
                new { IdUser = idUserInt });
        }

        [HttpGet("")]
        public async Task<IActionResult> Index()
        {
            await using var connection = _koneksi.GetConnection();
            await connection.OpenAsync();

            int? idCustomer = await GetIdCustomerAsync(connection);

            if (idCustomer == null)
                return Forbid();

            const string sql = @"
                SELECT
                    p.id_pemesanan AS IdPemesanan,
                    p.tgl_pesan AS TglPesan,
                    p.total_bayar AS TotalBayar,
                    p.status AS StatusPesanan,
                    COALESCE(pay.status, 'belum_bayar') AS StatusPembayaran,
                    COALESCE(mp.nama_metode, '-') AS NamaMetode
                FROM pemesanan p
                LEFT JOIN payments pay
                    ON pay.id_pemesanan = p.id_pemesanan
                LEFT JOIN metode_pembayaran mp
                    ON mp.id_metode = pay.id_metode
                WHERE p.id_customer = @IdCustomer
                ORDER BY p.tgl_pesan DESC";

            var pesanan = await connection.QueryAsync<PesananViewModel>(
                sql,
                new { IdCustomer = idCustomer.Value });

            return View(pesanan.ToList());
        }

        [HttpGet("Detail/{id:int}")]
        public async Task<IActionResult> Detail(int id)
        {
            await using var connection = _koneksi.GetConnection();
            await connection.OpenAsync();

            int? idCustomer = await GetIdCustomerAsync(connection);

            if (idCustomer == null)
                return Forbid();

            const string sqlPesanan = @"
                SELECT
                    p.id_pemesanan AS IdPemesanan,
                    p.tgl_pesan AS TglPesan,
                    p.subtotal AS Subtotal,
                    p.diskon AS Diskon,
                    p.ongkir AS Ongkir,
                    p.total_bayar AS TotalBayar,
                    p.status AS StatusPesanan,
                    COALESCE(pay.status, 'belum_bayar') AS StatusPembayaran,
                    COALESCE(mp.nama_metode, '-') AS NamaMetode,
                    pay.bukti_bayar AS BuktiBayar,
                    COALESCE(ac.penerima, '-') AS NamaPenerima,
                    COALESCE(ac.telepon, '-') AS Telepon,
                    COALESCE(ac.alamat_lengkap, '-') AS AlamatLengkap,
                    COALESCE(ac.kota, '-') AS Kota,
                    COALESCE(ac.provinsi, '-') AS Provinsi,
                    COALESCE(ac.kode_pos, '-') AS KodePos
                FROM pemesanan p
                LEFT JOIN payments pay
                    ON pay.id_pemesanan = p.id_pemesanan
                LEFT JOIN metode_pembayaran mp
                    ON mp.id_metode = pay.id_metode
                LEFT JOIN alamat_customer ac
                    ON ac.id_alamat = p.id_alamat
                WHERE p.id_pemesanan = @IdPemesanan
                  AND p.id_customer = @IdCustomer
                LIMIT 1";

            var pesanan = await connection.QueryFirstOrDefaultAsync<PesananSayaDetailViewModel>(
                sqlPesanan,
                new
                {
                    IdPemesanan = id,
                    IdCustomer = idCustomer.Value
                });

            if (pesanan == null)
                return NotFound();

            const string sqlDetail = @"
                SELECT
                    dp.id_detail_pesanan AS IdDetailPesanan,
                    dp.id_produk AS IdProduk,
                    pr.nama_produk AS NamaProduk,
                    pr.kode_produk AS KodeProduk,
                    pr.foto AS Foto,
                    dp.jumlah AS Jumlah,
                    dp.harga_satuan AS HargaSatuan,
                    dp.subtotal AS Subtotal
                FROM detail_pesanan dp
                INNER JOIN produk pr
                    ON pr.id_produk = dp.id_produk
                WHERE dp.id_pemesanan = @IdPemesanan";

            pesanan.Items = (await connection.QueryAsync<DetailPesananViewModel>(
                sqlDetail,
                new { IdPemesanan = id })).ToList();

            return View(pesanan);
        }

        [HttpPost("UploadBukti/{id:int}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadBukti(int id, IFormFile? file)
        {
            if (file == null || file.Length == 0)
            {
                TempData["Error"] = "Silakan pilih file bukti pembayaran.";
                return RedirectToAction(nameof(Detail), new { id });
            }

            if (file.Length > MaksimalUkuranFile)
            {
                TempData["Error"] = "Ukuran file maksimal 5 MB.";
                return RedirectToAction(nameof(Detail), new { id });
            }

            string extension = Path.GetExtension(file.FileName).ToLowerInvariant();

            if (extension != ".jpg" &&
                extension != ".jpeg" &&
                extension != ".png" &&
                extension != ".webp")
            {
                TempData["Error"] = "Format file harus JPG, JPEG, PNG, atau WEBP.";
                return RedirectToAction(nameof(Detail), new { id });
            }

            if (!await IsValidImageAsync(file, extension))
            {
                TempData["Error"] = "Isi file tidak sesuai dengan format gambar.";
                return RedirectToAction(nameof(Detail), new { id });
            }

            string? idUser = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (!int.TryParse(idUser, out int idUserInt))
                return Forbid();

            string folder = Path.Combine(
                _environment.WebRootPath,
                "uploads",
                "bukti-pembayaran");

            Directory.CreateDirectory(folder);

            string namaFile = $"{Guid.NewGuid():N}{extension}";
            string pathFile = Path.Combine(folder, namaFile);
            string pathDatabase = $"/uploads/bukti-pembayaran/{namaFile}";

            string? buktiLama = null;
            bool fileTersimpan = false;

            try
            {
                await using var connection = _koneksi.GetConnection();
                await connection.OpenAsync();

                await using var transaction =
                    await connection.BeginTransactionAsync();

                const string sqlCek = @"
                    SELECT
                        pay.bukti_bayar
                    FROM payments pay
                    INNER JOIN pemesanan p
                        ON p.id_pemesanan = pay.id_pemesanan
                    INNER JOIN customer c
                        ON c.id_customer = p.id_customer
                    WHERE pay.id_pemesanan = @IdPemesanan
                      AND c.id_user = @IdUser
                      AND p.status = 'menunggu_bayar'
                      AND pay.status IN ('pending', 'gagal')
                    LIMIT 1
                    FOR UPDATE";

                buktiLama = await connection.QueryFirstOrDefaultAsync<string?>(
                    sqlCek,
                    new
                    {
                        IdPemesanan = id,
                        IdUser = idUserInt
                    },
                    transaction);

                if (buktiLama == null)
                {
                    // Bukti lama yang kosong dan pesanan yang tidak memenuhi
                    // syarat sama-sama tidak menghasilkan file yang valid.
                    const string sqlValidasi = @"
                        SELECT COUNT(*)
                        FROM payments pay
                        INNER JOIN pemesanan p
                            ON p.id_pemesanan = pay.id_pemesanan
                        INNER JOIN customer c
                            ON c.id_customer = p.id_customer
                        WHERE pay.id_pemesanan = @IdPemesanan
                          AND c.id_user = @IdUser
                          AND p.status = 'menunggu_bayar'
                          AND pay.status IN ('pending', 'gagal')";

                    int jumlahValid = await connection.ExecuteScalarAsync<int>(
                        sqlValidasi,
                        new
                        {
                            IdPemesanan = id,
                            IdUser = idUserInt
                        },
                        transaction);

                    if (jumlahValid == 0)
                    {
                        await transaction.RollbackAsync();
                        TempData["Error"] =
                            "Pesanan tidak ditemukan atau tidak dapat menerima bukti pembayaran.";
                        return RedirectToAction(nameof(Detail), new { id });
                    }
                }

                await using (var stream = new FileStream(
                    pathFile,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None))
                {
                    await file.CopyToAsync(stream);
                }

                fileTersimpan = true;

                const string sqlUpdate = @"
                    UPDATE payments pay
                    INNER JOIN pemesanan p
                        ON p.id_pemesanan = pay.id_pemesanan
                    INNER JOIN customer c
                        ON c.id_customer = p.id_customer
                    SET
                        pay.bukti_bayar = @BuktiBayar,
                        pay.status = 'pending'
                    WHERE pay.id_pemesanan = @IdPemesanan
                      AND c.id_user = @IdUser
                      AND p.status = 'menunggu_bayar'
                      AND pay.status IN ('pending', 'gagal')";

                int affected = await connection.ExecuteAsync(
                    sqlUpdate,
                    new
                    {
                        BuktiBayar = pathDatabase,
                        IdPemesanan = id,
                        IdUser = idUserInt
                    },
                    transaction);

                if (affected == 0)
                {
                    await transaction.RollbackAsync();
                    System.IO.File.Delete(pathFile);

                    TempData["Error"] =
                        "Bukti pembayaran tidak dapat disimpan untuk pesanan ini.";

                    return RedirectToAction(nameof(Detail), new { id });
                }

                await transaction.CommitAsync();

                if (!string.IsNullOrWhiteSpace(buktiLama))
                {
                    HapusFileBuktiLama(buktiLama);
                }

                TempData["Success"] =
                    "Bukti pembayaran berhasil diunggah. Silakan tunggu verifikasi admin.";

                return RedirectToAction(nameof(Detail), new { id });
            }
            catch
            {
                if (fileTersimpan && System.IO.File.Exists(pathFile))
                    System.IO.File.Delete(pathFile);

                TempData["Error"] =
                    "Terjadi kesalahan saat mengunggah bukti pembayaran.";

                return RedirectToAction(nameof(Detail), new { id });
            }
        }

        private static async Task<bool> IsValidImageAsync(
            IFormFile file,
            string extension)
        {
            byte[] header = new byte[12];

            await using var stream = file.OpenReadStream();
            int bytesRead = await stream.ReadAsync(header, 0, header.Length);

            if (extension == ".jpg" || extension == ".jpeg")
            {
                return bytesRead >= 3 &&
                       header[0] == 0xFF &&
                       header[1] == 0xD8 &&
                       header[2] == 0xFF;
            }

            if (extension == ".png")
            {
                return bytesRead >= 8 &&
                       header[0] == 0x89 &&
                       header[1] == 0x50 &&
                       header[2] == 0x4E &&
                       header[3] == 0x47 &&
                       header[4] == 0x0D &&
                       header[5] == 0x0A &&
                       header[6] == 0x1A &&
                       header[7] == 0x0A;
            }

            if (extension == ".webp")
            {
                return bytesRead >= 12 &&
                       header[0] == (byte)'R' &&
                       header[1] == (byte)'I' &&
                       header[2] == (byte)'F' &&
                       header[3] == (byte)'F' &&
                       header[8] == (byte)'W' &&
                       header[9] == (byte)'E' &&
                       header[10] == (byte)'B' &&
                       header[11] == (byte)'P';
            }

            return false;
        }

        private void HapusFileBuktiLama(string pathDatabase)
        {
            try
            {
                string namaFile = Path.GetFileName(pathDatabase);

                string path = Path.Combine(
                    _environment.WebRootPath,
                    "uploads",
                    "bukti-pembayaran",
                    namaFile);

                if (System.IO.File.Exists(path))
                    System.IO.File.Delete(path);
            }
            catch
            {
                // Kegagalan menghapus file lama tidak membatalkan upload baru.
            }
        }
    }
}
