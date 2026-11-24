
using System.Security.Claims;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TokoMusik.Web.Data;
using TokoMusik.Web.Models;

namespace TokoMusik.Web.Controllers
{
    [Authorize(Roles = "user")]
    public class ReturController : Controller
    {
        private readonly Koneksi _koneksi;
        private readonly IWebHostEnvironment _environment;

        private const long MaksimalUkuranFoto = 5 * 1024 * 1024;

        public ReturController(
            Koneksi koneksi,
            IWebHostEnvironment environment)
        {
            _koneksi = koneksi;
            _environment = environment;
        }

        private int IdUser =>
            int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int id)
                ? id : 0;

        private async Task<int> AmbilIdCustomer()
        {
            await using var connection = _koneksi.GetConnection();

            return await connection.ExecuteScalarAsync<int?>(
                "SELECT id_customer FROM customer WHERE id_user = @IdUser",
                new { IdUser }) ?? 0;
        }

        private async Task<ProdukReturViewModel?> AmbilProdukRetur(
            System.Data.IDbConnection connection,
            int idDetailPesanan,
            int idCustomer,
            System.Data.IDbTransaction? transaction = null,
            bool kunciBaris = false)
        {
            string sql = @"
                SELECT
                    dp.id_detail_pesanan AS IdDetailPesanan,
                    dp.id_pemesanan AS IdPemesanan,
                    p.kode_produk AS KodeProduk,
                    p.nama_produk AS NamaProduk,
                    dp.jumlah AS JumlahDibeli,

                    dp.jumlah - COALESCE((
                        SELECT SUM(r.jumlah)
                        FROM return_produk r
                        WHERE r.id_detail_pesanan = dp.id_detail_pesanan
                          AND r.status <> 'ditolak'
                    ), 0) AS SisaJumlah,

                    pm.status AS StatusPesanan,
                    pg.waktu_terima AS WaktuTerima

                FROM detail_pesanan dp

                INNER JOIN pemesanan pm
                    ON pm.id_pemesanan = dp.id_pemesanan

                INNER JOIN produk p
                    ON p.id_produk = dp.id_produk

                INNER JOIN pengiriman pg
                    ON pg.id_pemesanan = pm.id_pemesanan

                WHERE dp.id_detail_pesanan = @IdDetailPesanan
                  AND pm.id_customer = @IdCustomer
                  AND pm.status = 'selesai'

                  AND pg.waktu_terima IS NOT NULL
                  AND pg.waktu_terima <= NOW()
                  AND NOW() < DATE_ADD(
                      pg.waktu_terima,
                      INTERVAL 72 HOUR
                  )

                  AND NOT EXISTS (
                      SELECT 1
                      FROM return_produk r
                      WHERE r.id_detail_pesanan = dp.id_detail_pesanan
                        AND r.status <> 'ditolak'
                  )

                  AND NOT EXISTS (
                      SELECT 1
                      FROM return_produk r
                      WHERE r.id_detail_pesanan = dp.id_detail_pesanan
                        AND r.status = 'ditolak'
                        AND r.tgl_pengajuan >= CURDATE()
                        AND r.tgl_pengajuan < DATE_ADD(
                            CURDATE(),
                            INTERVAL 1 DAY
                        )
                  )";

            if (kunciBaris)
                sql += " FOR UPDATE";

            return await connection.QueryFirstOrDefaultAsync<ProdukReturViewModel>(
                sql,
                new
                {
                    IdDetailPesanan = idDetailPesanan,
                    IdCustomer = idCustomer
                },
                transaction);
        }

        private async Task<bool> FotoValid(IFormFile? file)
        {
            if (file == null || file.Length <= 0 ||
                file.Length > MaksimalUkuranFoto)
                return false;

            string ekstensi = Path.GetExtension(file.FileName).ToLowerInvariant();

            if (ekstensi is not ".jpg" and not ".jpeg" and not ".png" and not ".webp")
                return false;

            byte[] header = new byte[12];

            await using var stream = file.OpenReadStream();
            int dibaca = await stream.ReadAsync(header, 0, header.Length);

            bool jpeg = dibaca >= 3 &&
                        header[0] == 0xFF &&
                        header[1] == 0xD8 &&
                        header[2] == 0xFF;

            bool png = dibaca >= 8 &&
                       header[0] == 0x89 &&
                       header[1] == 0x50 &&
                       header[2] == 0x4E &&
                       header[3] == 0x47 &&
                       header[4] == 0x0D &&
                       header[5] == 0x0A &&
                       header[6] == 0x1A &&
                       header[7] == 0x0A;

            bool webp = dibaca >= 12 &&
                        header[0] == 'R' &&
                        header[1] == 'I' &&
                        header[2] == 'F' &&
                        header[3] == 'F' &&
                        header[8] == 'W' &&
                        header[9] == 'E' &&
                        header[10] == 'B' &&
                        header[11] == 'P';

            return jpeg || png || webp;
        }

        private async Task<string> SimpanFoto(IFormFile file)
        {
            string webRoot = _environment.WebRootPath
                ?? Path.Combine(_environment.ContentRootPath, "wwwroot");

            string folder = Path.Combine(
                webRoot,
                "uploads",
                "bukti-retur");

            Directory.CreateDirectory(folder);

            string ekstensi = Path.GetExtension(file.FileName).ToLowerInvariant();
            string namaFile = $"{Guid.NewGuid():N}{ekstensi}";
            string path = Path.Combine(folder, namaFile);

            await using var stream = new FileStream(path, FileMode.CreateNew);
            await file.CopyToAsync(stream);

            return $"/uploads/bukti-retur/{namaFile}";
        }

        private void HapusFoto(string? foto)
        {
            if (string.IsNullOrWhiteSpace(foto))
                return;

            string webRoot = _environment.WebRootPath
                ?? Path.Combine(_environment.ContentRootPath, "wwwroot");

            string namaFile = Path.GetFileName(foto);
            string path = Path.Combine(
                webRoot,
                "uploads",
                "bukti-retur",
                namaFile);

            if (System.IO.File.Exists(path))
                System.IO.File.Delete(path);
        }

        // GET: /Retur
        public async Task<IActionResult> Index()
        {
            int idCustomer = await AmbilIdCustomer();

            if (idCustomer == 0)
                return Forbid();

            await using var connection = _koneksi.GetConnection();
            await connection.OpenAsync();

            var daftarRetur = await connection.QueryAsync<ReturItemViewModel>(
                @"
                SELECT
                    r.id_return AS IdReturn,
                    r.id_detail_pesanan AS IdDetailPesanan,
                    r.tgl_return AS TanggalReturn,
                    r.tgl_pengajuan AS TanggalPengajuan,

                    dp.id_pemesanan AS IdPemesanan,
                    p.kode_produk AS KodeProduk,
                    p.nama_produk AS NamaProduk,

                    r.jumlah AS Jumlah,
                    r.alasan AS Alasan,
                    r.foto_bukti AS FotoBukti,
                    r.status AS Status,
                    r.barang_diterima AS BarangDiterima,
                    r.layak_jual AS LayakJual,
                    r.stok_ditambahkan AS StokDitambahkan,
                    r.tgl_pemeriksaan AS TanggalPemeriksaan,
                    r.catatan_admin AS CatatanAdmin

                FROM return_produk r

                INNER JOIN detail_pesanan dp
                    ON dp.id_detail_pesanan = r.id_detail_pesanan

                INNER JOIN pemesanan pm
                    ON pm.id_pemesanan = dp.id_pemesanan

                INNER JOIN produk p
                    ON p.id_produk = dp.id_produk

                WHERE pm.id_customer = @IdCustomer

                ORDER BY r.tgl_pengajuan DESC, r.id_return DESC",
                new { IdCustomer = idCustomer });

            var produkDapatDiretur = await connection.QueryAsync<ProdukReturViewModel>(
                @"
                SELECT
                    dp.id_detail_pesanan AS IdDetailPesanan,
                    dp.id_pemesanan AS IdPemesanan,

                    p.kode_produk AS KodeProduk,
                    p.nama_produk AS NamaProduk,

                    dp.jumlah AS JumlahDibeli,

                    COALESCE((
                        SELECT SUM(r.jumlah)
                        FROM return_produk r
                        WHERE r.id_detail_pesanan = dp.id_detail_pesanan
                          AND r.status <> 'ditolak'
                    ), 0) AS JumlahSudahDiretur,

                    dp.jumlah - COALESCE((
                        SELECT SUM(r.jumlah)
                        FROM return_produk r
                        WHERE r.id_detail_pesanan = dp.id_detail_pesanan
                          AND r.status <> 'ditolak'
                    ), 0) AS SisaJumlah,

                    pm.status AS StatusPesanan,
                    pg.waktu_terima AS WaktuTerima

                FROM detail_pesanan dp

                INNER JOIN pemesanan pm
                    ON pm.id_pemesanan = dp.id_pemesanan

                INNER JOIN pengiriman pg
                    ON pg.id_pemesanan = pm.id_pemesanan

                INNER JOIN produk p
                    ON p.id_produk = dp.id_produk

                WHERE pm.id_customer = @IdCustomer
                  AND pm.status = 'selesai'

                  AND pg.waktu_terima IS NOT NULL
                  AND pg.waktu_terima <= NOW()
                  AND NOW() < DATE_ADD(
                      pg.waktu_terima,
                      INTERVAL 72 HOUR
                  )

                  AND dp.jumlah > COALESCE((
                      SELECT SUM(r.jumlah)
                      FROM return_produk r
                      WHERE r.id_detail_pesanan = dp.id_detail_pesanan
                        AND r.status <> 'ditolak'
                  ), 0)

                  AND NOT EXISTS (
                      SELECT 1
                      FROM return_produk r
                      WHERE r.id_detail_pesanan = dp.id_detail_pesanan
                        AND r.status <> 'ditolak'
                  )

                  AND NOT EXISTS (
                      SELECT 1
                      FROM return_produk r
                      WHERE r.id_detail_pesanan = dp.id_detail_pesanan
                        AND r.status = 'ditolak'
                        AND r.tgl_pengajuan >= CURDATE()
                        AND r.tgl_pengajuan < DATE_ADD(
                            CURDATE(),
                            INTERVAL 1 DAY
                        )
                  )

                ORDER BY pg.waktu_terima DESC",
                new { IdCustomer = idCustomer });

            return View(new ReturViewModel
            {
                DaftarRetur = daftarRetur.ToList(),
                ProdukDapatDiretur = produkDapatDiretur.ToList()
            });
        }

        // GET: /Retur/Ajukan/5
        [HttpGet]
        public async Task<IActionResult> Ajukan(int id)
        {
            int idCustomer = await AmbilIdCustomer();

            if (idCustomer == 0)
                return Forbid();

            await using var connection = _koneksi.GetConnection();
            await connection.OpenAsync();

            var produk = await AmbilProdukRetur(
                connection,
                id,
                idCustomer);

            if (produk == null || produk.SisaJumlah <= 0)
            {
                TempData["Error"] =
                    "Produk tidak memenuhi syarat retur. Pastikan batas waktu 3 x 24 jam belum berakhir, tidak ada pengajuan aktif, dan jika pengajuan sebelumnya ditolak, pengajuan ulang hanya dapat dilakukan pada hari berikutnya.";

                return RedirectToAction(nameof(Index));
            }

            return View(new AjukanReturViewModel
            {
                IdDetailPesanan = produk.IdDetailPesanan,
                NamaProduk = produk.NamaProduk,
                KodeProduk = produk.KodeProduk,
                SisaJumlah = produk.SisaJumlah,
                Jumlah = 1
            });
        }

        // POST: /Retur/Ajukan
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Ajukan(AjukanReturViewModel model)
        {
            int idCustomer = await AmbilIdCustomer();

            if (idCustomer == 0)
                return Forbid();

            if (!await FotoValid(model.FotoBukti))
            {
                ModelState.AddModelError(
                    "FotoBukti",
                    "Foto wajib JPG, JPEG, PNG, atau WEBP dengan ukuran maksimal 5 MB.");
            }

            await using var connection = _koneksi.GetConnection();
            await connection.OpenAsync();

            await using var transaction = await connection.BeginTransactionAsync();

            string? fotoTersimpan = null;

            try
            {
                // Kunci detail pesanan untuk mencegah pengajuan bersamaan.
                int? detailTerkunci = await connection.QueryFirstOrDefaultAsync<int?>(
                    @"
                    SELECT dp.id_detail_pesanan
                    FROM detail_pesanan dp
                    INNER JOIN pemesanan pm
                        ON pm.id_pemesanan = dp.id_pemesanan
                    WHERE dp.id_detail_pesanan = @IdDetailPesanan
                      AND pm.id_customer = @IdCustomer
                    FOR UPDATE",
                    new
                    {
                        model.IdDetailPesanan,
                        IdCustomer = idCustomer
                    },
                    transaction);

                if (!detailTerkunci.HasValue)
                {
                    await transaction.RollbackAsync();

                    TempData["Error"] =
                        "Detail pesanan tidak ditemukan atau bukan milik akun kamu.";

                    return RedirectToAction(nameof(Index));
                }

                // Validasi ulang di dalam transaksi.
                // Termasuk batas 72 jam dan larangan pengajuan ulang
                // pada hari yang sama setelah pengajuan sebelumnya ditolak.
                var produk = await AmbilProdukRetur(
                    connection,
                    model.IdDetailPesanan,
                    idCustomer,
                    transaction);

                if (produk == null || produk.SisaJumlah <= 0)
                {
                    await transaction.RollbackAsync();

                    TempData["Error"] =
                        "Pengajuan retur belum dapat dilakukan. Jika pengajuan sebelumnya ditolak hari ini, silakan coba kembali besok. Batas pengajuan tetap 3 x 24 jam sejak barang diterima.";

                    return RedirectToAction(nameof(Index));
                }

                model.NamaProduk = produk.NamaProduk;
                model.KodeProduk = produk.KodeProduk;
                model.SisaJumlah = produk.SisaJumlah;

                if (model.Jumlah < 1 || model.Jumlah > produk.SisaJumlah)
                {
                    ModelState.AddModelError(
                        "Jumlah",
                        "Jumlah retur harus minimal 1 dan tidak boleh melebihi jumlah produk yang dapat diretur.");
                }

                if (string.IsNullOrWhiteSpace(model.Alasan))
                {
                    ModelState.AddModelError(
                        "Alasan",
                        "Alasan retur wajib diisi.");
                }

                if (!ModelState.IsValid)
                {
                    await transaction.RollbackAsync();
                    return View(model);
                }

                fotoTersimpan = await SimpanFoto(model.FotoBukti!);

                await connection.ExecuteAsync(
                    @"
                    INSERT INTO return_produk
                        (id_detail_pesanan, tgl_return, jumlah, alasan, foto_bukti)
                    VALUES
                        (@IdDetailPesanan, CURDATE(), @Jumlah, @Alasan, @FotoBukti)",
                    new
                    {
                        model.IdDetailPesanan,
                        model.Jumlah,
                        Alasan = model.Alasan.Trim(),
                        FotoBukti = fotoTersimpan
                    },
                    transaction);

                await transaction.CommitAsync();

                TempData["Success"] =
                    "Pengajuan retur berhasil dikirim dan sedang menunggu pemeriksaan admin.";

                return RedirectToAction(nameof(Index));
            }
            catch
            {
                await transaction.RollbackAsync();

                if (fotoTersimpan != null)
                    HapusFoto(fotoTersimpan);

                throw;
            }
        }
    }
}
