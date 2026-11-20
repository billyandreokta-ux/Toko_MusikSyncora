
using System.Security.Claims;
using System.Text.Json;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MySqlConnector;
using TokoMusik.Web.Data;
using TokoMusik.Web.Models;

namespace TokoMusik.Web.Controllers
{
    [Authorize(Roles = "user")]
    public class CheckoutController : Controller
    {
        private readonly Koneksi _koneksi;

        private const string SessionKey = "TOKOMUSIK_KERANJANG";

        public CheckoutController(Koneksi koneksi)
        {
            _koneksi = koneksi;
        }

        private List<KeranjangItem> AmbilKeranjang()
        {
            string? json = HttpContext.Session.GetString(SessionKey);

            if (string.IsNullOrWhiteSpace(json))
                return new List<KeranjangItem>();

            return JsonSerializer.Deserialize<List<KeranjangItem>>(json)
                   ?? new List<KeranjangItem>();
        }

        private void KosongkanKeranjang()
        {
            HttpContext.Session.Remove(SessionKey);
        }

        private async Task<int?> AmbilIdCustomer(
            MySqlConnection connection)
        {
            string? idUser = User.FindFirstValue(
                ClaimTypes.NameIdentifier
            );

            if (!int.TryParse(idUser, out int userId))
                return null;

            return await connection.QueryFirstOrDefaultAsync<int?>(
                @"
                SELECT id_customer
                FROM customer
                WHERE id_user = @IdUser
                ",
                new { IdUser = userId }
            );
        }

        // GET: /Checkout
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var keranjang = AmbilKeranjang();

            if (keranjang.Count == 0)
            {
                TempData["Error"] = "Keranjang belanja masih kosong.";
                return RedirectToAction("Index", "Keranjang");
            }

            using var connection = _koneksi.GetConnection();
            await connection.OpenAsync();

            int? idCustomer = await AmbilIdCustomer(connection);

            if (idCustomer == null)
                return RedirectToAction("Index", "User");

            var alamat = (await connection.QueryAsync<AlamatCustomer>(
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
            )).ToList();

            if (alamat.Count == 0)
            {
                TempData["Error"] =
                    "Tambahkan alamat pengiriman terlebih dahulu.";

                return RedirectToAction("Tambah", "Alamat");
            }

            var metode = (await connection.QueryAsync<MetodePembayaranViewModel>(
                @"
                SELECT
                    id_metode AS IdMetode,
                    nama_metode AS NamaMetode,
                    keterangan AS Keterangan
                FROM metode_pembayaran
                ORDER BY id_metode
                "
            )).ToList();

            if (metode.Count == 0)
            {
                TempData["Error"] =
                    "Belum ada metode pembayaran yang tersedia.";

                return RedirectToAction("Index", "Keranjang");
            }

            var model = new CheckoutViewModel
            {
                Alamat = alamat,
                MetodePembayaran = metode,
                Ongkir = 0,
                Diskon = 0
            };

            foreach (var item in keranjang)
            {
                var produk = await connection.QueryFirstOrDefaultAsync<CheckoutItemViewModel>(
                    @"
                    SELECT
                        p.id_produk AS IdProduk,
                        p.nama_produk AS NamaProduk,
                        p.kode_produk AS KodeProduk,
                        p.foto AS Foto,
                        p.harga_jual AS HargaJual,
                        COALESCE(s.jumlah, 0) AS Stok
                    FROM produk p
                    LEFT JOIN stock s ON s.id_produk = p.id_produk
                    WHERE p.id_produk = @IdProduk
                      AND p.status = 'aktif'
                    ",
                    new { item.IdProduk }
                );

                if (produk == null || produk.Stok <= 0)
                {
                    TempData["Error"] =
                        "Ada produk dalam keranjang yang sudah tidak tersedia.";

                    return RedirectToAction("Index", "Keranjang");
                }

                if (item.Jumlah > produk.Stok)
                {
                    TempData["Error"] =
                        $"Stok {produk.NamaProduk} hanya tersedia {produk.Stok} unit. Perbarui keranjang terlebih dahulu.";

                    return RedirectToAction("Index", "Keranjang");
                }

                produk.Jumlah = item.Jumlah;

                model.Items.Add(produk);
                model.Subtotal += produk.Subtotal;
            }

            model.IdAlamat =
                alamat.FirstOrDefault(x => x.IsUtama)?.IdAlamat
                ?? alamat.First().IdAlamat;

            model.IdMetode = metode.First().IdMetode;

            return View(model);
        }

        // POST: /Checkout/Proses
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Proses(
            int idAlamat,
            int idMetode)
        {
            var keranjang = AmbilKeranjang();

            if (keranjang.Count == 0)
            {
                TempData["Error"] = "Keranjang belanja masih kosong.";
                return RedirectToAction("Index", "Keranjang");
            }

            string? idUser = User.FindFirstValue(
                ClaimTypes.NameIdentifier
            );

            if (!int.TryParse(idUser, out int userId))
                return RedirectToAction("Login", "Auth");

            using var connection = _koneksi.GetConnection();
            await connection.OpenAsync();

            await using var transaction =
                await connection.BeginTransactionAsync();

            try
            {
                // 1. Ambil customer berdasarkan akun yang login
                int? idCustomer =
                    await connection.QueryFirstOrDefaultAsync<int?>(
                        @"
                        SELECT id_customer
                        FROM customer
                        WHERE id_user = @IdUser
                        ",
                        new { IdUser = userId },
                        transaction
                    );

                if (idCustomer == null)
                    throw new InvalidOperationException(
                        "Data pelanggan tidak ditemukan."
                    );

                // 2. Pastikan alamat merupakan milik pelanggan
                int alamatValid =
                    await connection.ExecuteScalarAsync<int>(
                        @"
                        SELECT COUNT(*)
                        FROM alamat_customer
                        WHERE id_alamat = @IdAlamat
                          AND id_customer = @IdCustomer
                        ",
                        new
                        {
                            IdAlamat = idAlamat,
                            IdCustomer = idCustomer
                        },
                        transaction
                    );

                if (alamatValid == 0)
                    throw new InvalidOperationException(
                        "Alamat pengiriman tidak valid."
                    );

                // 3. Pastikan metode pembayaran tersedia
                int metodeValid =
                    await connection.ExecuteScalarAsync<int>(
                        @"
                        SELECT COUNT(*)
                        FROM metode_pembayaran
                        WHERE id_metode = @IdMetode
                        ",
                        new { IdMetode = idMetode },
                        transaction
                    );

                if (metodeValid == 0)
                    throw new InvalidOperationException(
                        "Metode pembayaran tidak valid."
                    );

                // 4. Periksa stok dan harga terbaru dengan FOR UPDATE
                decimal subtotal = 0;

                var produkPesanan =
                    new List<CheckoutItemViewModel>();

                foreach (var item in keranjang)
                {
                    var produk =
                        await connection.QueryFirstOrDefaultAsync<CheckoutItemViewModel>(
                            @"
                            SELECT
                                p.id_produk AS IdProduk,
                                p.nama_produk AS NamaProduk,
                                p.kode_produk AS KodeProduk,
                                p.harga_jual AS HargaJual,
                                COALESCE(s.jumlah, 0) AS Stok
                            FROM produk p
                            INNER JOIN stock s
                                ON s.id_produk = p.id_produk
                            WHERE p.id_produk = @IdProduk
                              AND p.status = 'aktif'
                            FOR UPDATE
                            ",
                            new { item.IdProduk },
                            transaction
                        );

                    if (produk == null)
                        throw new InvalidOperationException(
                            "Salah satu produk sudah tidak tersedia."
                        );

                    if (item.Jumlah <= 0 ||
                        item.Jumlah > produk.Stok)
                    {
                        throw new InvalidOperationException(
                            $"Stok {produk.NamaProduk} tidak mencukupi. Stok tersedia: {produk.Stok}."
                        );
                    }

                    produk.Jumlah = item.Jumlah;

                    subtotal += produk.Subtotal;
                    produkPesanan.Add(produk);
                }

                // 5. Hitung total pesanan
                decimal diskon = 0;
                decimal ongkir = 0;
                decimal totalBayar = subtotal + ongkir - diskon;

                // 6. Simpan data pemesanan
                await connection.ExecuteAsync(
                    @"
                    INSERT INTO pemesanan
                    (
                        id_customer,
                        id_alamat,
                        id_promo,
                        tgl_pesan,
                        subtotal,
                        diskon,
                        ongkir,
                        total_bayar,
                        status
                    )
                    VALUES
                    (
                        @IdCustomer,
                        @IdAlamat,
                        NULL,
                        NOW(),
                        @Subtotal,
                        @Diskon,
                        @Ongkir,
                        @TotalBayar,
                        'menunggu_bayar'
                    )
                    ",
                    new
                    {
                        IdCustomer = idCustomer,
                        IdAlamat = idAlamat,
                        Subtotal = subtotal,
                        Diskon = diskon,
                        Ongkir = ongkir,
                        TotalBayar = totalBayar
                    },
                    transaction
                );

                int idPemesanan =
                    await connection.ExecuteScalarAsync<int>(
                        "SELECT LAST_INSERT_ID()",
                        transaction: transaction
                    );

                // 7. Simpan detail produk dan kurangi stok
                foreach (var produk in produkPesanan)
                {
                    await connection.ExecuteAsync(
                        @"
                        INSERT INTO detail_pesanan
                        (
                            id_pemesanan,
                            id_produk,
                            jumlah,
                            harga_satuan
                        )
                        VALUES
                        (
                            @IdPemesanan,
                            @IdProduk,
                            @Jumlah,
                            @HargaSatuan
                        )
                        ",
                        new
                        {
                            IdPemesanan = idPemesanan,
                            produk.IdProduk,
                            produk.Jumlah,
                            HargaSatuan = produk.HargaJual
                        },
                        transaction
                    );

                    int stokDikurangi =
                        await connection.ExecuteAsync(
                            @"
                            UPDATE stock
                            SET jumlah = jumlah - @Jumlah
                            WHERE id_produk = @IdProduk
                              AND jumlah >= @Jumlah
                            ",
                            new
                            {
                                produk.IdProduk,
                                produk.Jumlah
                            },
                            transaction
                        );

                    if (stokDikurangi != 1)
                        throw new InvalidOperationException(
                            $"Gagal memperbarui stok {produk.NamaProduk}."
                        );
                }

                // 8. Catat pembayaran dengan status pending
                await connection.ExecuteAsync(
                    @"
                    INSERT INTO payments
                    (
                        id_pemesanan,
                        id_metode,
                        tgl_bayar,
                        jumlah_bayar,
                        bukti_bayar,
                        status
                    )
                    VALUES
                    (
                        @IdPemesanan,
                        @IdMetode,
                        NOW(),
                        @JumlahBayar,
                        NULL,
                        'pending'
                    )
                    ",
                    new
                    {
                        IdPemesanan = idPemesanan,
                        IdMetode = idMetode,
                        JumlahBayar = totalBayar
                    },
                    transaction
                );

                // 9. Simpan seluruh perubahan
                await transaction.CommitAsync();

                // Kosongkan keranjang setelah transaksi berhasil
                KosongkanKeranjang();

                TempData["Success"] =
                    "Pesanan berhasil dibuat. Silakan lanjutkan pembayaran.";

                return RedirectToAction(
                    "Selesai",
                    new { id = idPemesanan }
                );
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();

                TempData["Error"] =
                    ex is MySqlException
                        ? "Terjadi kesalahan database saat membuat pesanan."
                        : ex.Message;

                return RedirectToAction("Index");
            }
        }

        // GET: /Checkout/Selesai/1
        [HttpGet]
        public async Task<IActionResult> Selesai(int id)
        {
            using var connection = _koneksi.GetConnection();
            await connection.OpenAsync();

            int? idCustomer = await AmbilIdCustomer(connection);

            if (idCustomer == null)
                return RedirectToAction("Index", "User");

            var pesanan =
                await connection.QueryFirstOrDefaultAsync<CheckoutSelesaiViewModel>(
                    @"
                    SELECT
                        p.id_pemesanan AS IdPemesanan,
                        p.tgl_pesan AS TanggalPesan,
                        p.status AS StatusPesanan,
                        p.subtotal AS Subtotal,
                        p.ongkir AS Ongkir,
                        p.diskon AS Diskon,
                        p.total_bayar AS TotalBayar,

                        a.penerima AS NamaPenerima,
                        a.telepon AS Telepon,
                        a.alamat_lengkap AS AlamatLengkap,
                        a.kota AS Kota,
                        a.provinsi AS Provinsi,
                        a.kode_pos AS KodePos,

                        mp.nama_metode AS NamaMetode

                    FROM pemesanan p

                    INNER JOIN alamat_customer a
                        ON a.id_alamat = p.id_alamat

                    INNER JOIN payments pay
                        ON pay.id_pemesanan = p.id_pemesanan

                    INNER JOIN metode_pembayaran mp
                        ON mp.id_metode = pay.id_metode

                    WHERE p.id_pemesanan = @IdPemesanan
                      AND p.id_customer = @IdCustomer
                    ORDER BY pay.id_payment DESC
                    LIMIT 1
                    ",
                    new
                    {
                        IdPemesanan = id,
                        IdCustomer = idCustomer
                    }
                );

            if (pesanan == null)
                return NotFound();

            return View(pesanan);
        }
    }
}
