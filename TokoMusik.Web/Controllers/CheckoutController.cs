
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
        private const string SessionCheckoutKey = "SYNCORA_CHECKOUT_ITEMS";

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

        private void SimpanKeranjang(List<KeranjangItem> keranjang)
        {
            HttpContext.Session.SetString(
                SessionKey,
                JsonSerializer.Serialize(keranjang)
            );
        }

        private List<KeranjangItem> AmbilItemCheckout()
        {
            string? json = HttpContext.Session.GetString(SessionCheckoutKey);

            if (string.IsNullOrWhiteSpace(json))
                return new List<KeranjangItem>();

            return JsonSerializer.Deserialize<List<KeranjangItem>>(json)
                ?? new List<KeranjangItem>();
        }

        private void SimpanItemCheckout(List<KeranjangItem> items)
        {
            HttpContext.Session.SetString(
                SessionCheckoutKey,
                JsonSerializer.Serialize(items)
            );
        }

        private void HapusItemCheckout()
        {
            HttpContext.Session.Remove(SessionCheckoutKey);
        }

        private void HapusProdukYangDibeli(List<KeranjangItem> produkDibeli)
        {
            var keranjang = AmbilKeranjang();

            var idProdukDibeli = produkDibeli
                .Select(x => x.IdProduk)
                .ToHashSet();

            keranjang.RemoveAll(x => idProdukDibeli.Contains(x.IdProduk));

            SimpanKeranjang(keranjang);
        }

        private async Task<int?> AmbilIdCustomerAsync(
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

        // POST: /Checkout/Mulai
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Mulai(int[] selectedIds)
        {
            if (selectedIds == null || selectedIds.Length == 0)
            {
                TempData["Error"] =
                    "Pilih minimal satu produk yang ingin di-checkout.";

                return RedirectToAction("Index", "Keranjang");
            }

            var keranjang = AmbilKeranjang();

            var pilihan = selectedIds
                .Distinct()
                .Select(id => keranjang.FirstOrDefault(x => x.IdProduk == id))
                .Where(x => x != null)
                .Select(x => new KeranjangItem
                {
                    IdProduk = x!.IdProduk,
                    Jumlah = x.Jumlah
                })
                .ToList();

            if (pilihan.Count == 0)
            {
                TempData["Error"] =
                    "Produk yang dipilih tidak ditemukan di keranjang.";

                return RedirectToAction("Index", "Keranjang");
            }

            SimpanItemCheckout(pilihan);

            return RedirectToAction(nameof(Index));
        }

        // POST: /Checkout/BeliSekarang
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BeliSekarang(
            int idProduk,
            int jumlah = 1)
        {
            if (idProduk <= 0 || jumlah <= 0)
            {
                TempData["Error"] = "Jumlah pembelian tidak valid.";

                return RedirectToAction(
                    "Detail",
                    "Produk",
                    new { id = idProduk }
                );
            }

            await using var connection = _koneksi.GetConnection();
            await connection.OpenAsync();

            var produk = await connection.QueryFirstOrDefaultAsync<dynamic>(
                @"
                SELECT
                    p.id_produk AS IdProduk,
                    p.nama_produk AS NamaProduk,
                    COALESCE(s.jumlah, 0) AS Stok
                FROM produk p
                LEFT JOIN stock s ON s.id_produk = p.id_produk
                WHERE p.id_produk = @IdProduk
                    AND p.status = 'aktif'
                ",
                new { IdProduk = idProduk }
            );

            if (produk == null)
            {
                TempData["Error"] = "Produk tidak ditemukan.";

                return RedirectToAction("Index", "Produk");
            }

            int stok = (int)produk.Stok;

            if (stok <= 0)
            {
                TempData["Error"] = "Stok produk sedang habis.";

                return RedirectToAction(
                    "Detail",
                    "Produk",
                    new { id = idProduk }
                );
            }

            if (jumlah > stok)
            {
                TempData["Error"] =
                    $"Stok {produk.NamaProduk} hanya tersedia {stok} unit.";

                return RedirectToAction(
                    "Detail",
                    "Produk",
                    new { id = idProduk }
                );
            }

            SimpanItemCheckout(new List<KeranjangItem>
            {
                new KeranjangItem
                {
                    IdProduk = idProduk,
                    Jumlah = jumlah
                }
            });

            return RedirectToAction(nameof(Index));
        }

        private async Task<CheckoutViewModel> BangunModelCheckoutAsync(
            MySqlConnection connection,
            List<KeranjangItem> keranjang,
            int idCustomer,
            int? idAlamat,
            int? idMetode,
            string? kodePromo)
        {
            var model = new CheckoutViewModel
            {
                KodePromo = kodePromo?.Trim().ToUpperInvariant()
            };

            // Hanya mengambil alamat yang masih aktif.
            model.Alamat = (await connection.QueryAsync<AlamatCustomer>(
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
                    AND is_aktif = 1
                ORDER BY is_utama DESC, id_alamat DESC
                ",
                new { IdCustomer = idCustomer }
            )).ToList();

            model.MetodePembayaran =
                (await connection.QueryAsync<MetodePembayaranViewModel>(
                    @"
                    SELECT
                        id_metode AS IdMetode,
                        nama_metode AS NamaMetode,
                        keterangan AS Keterangan
                    FROM metode_pembayaran
                    ORDER BY id_metode
                    "
                )).ToList();

            if (model.Alamat.Count == 0 ||
                model.MetodePembayaran.Count == 0)
            {
                return model;
            }

            var alamatTerpilih = idAlamat.HasValue
                ? model.Alamat.FirstOrDefault(
                    x => x.IdAlamat == idAlamat.Value)
                : null;

            alamatTerpilih ??=
                model.Alamat.FirstOrDefault(x => x.IsUtama)
                ?? model.Alamat.First();

            model.IdAlamat = alamatTerpilih.IdAlamat;

            model.IdMetode = model.MetodePembayaran.Any(
                x => x.IdMetode == idMetode)
                ? idMetode!.Value
                : model.MetodePembayaran.First().IdMetode;

            foreach (var item in keranjang)
            {
                var produk =
                    await connection.QueryFirstOrDefaultAsync<CheckoutItemViewModel>(
                        @"
                        SELECT
                            p.id_produk AS IdProduk,
                            p.nama_produk AS NamaProduk,
                            p.kode_produk AS KodeProduk,
                            p.foto AS Foto,
                            p.harga_jual AS HargaJual,
                            COALESCE(s.jumlah, 0) AS Stok
                        FROM produk p
                        LEFT JOIN stock s
                            ON s.id_produk = p.id_produk
                        WHERE p.id_produk = @IdProduk
                            AND p.status = 'aktif'
                        ",
                        new { item.IdProduk }
                    );

                if (produk == null || produk.Stok <= 0)
                    continue;

                produk.Jumlah = Math.Min(
                    Math.Max(item.Jumlah, 0),
                    produk.Stok
                );

                if (produk.Jumlah <= 0)
                    continue;

                model.Items.Add(produk);
                model.Subtotal += produk.Subtotal;
            }

            if (model.Items.Count == 0)
                return model;

            const string sqlTarif = @"
                SELECT
                    biaya AS Biaya,
                    estimasi_hari AS EstimasiHari
                FROM tarif_pengiriman
                WHERE status = 'aktif'
                    AND (
                        (
                            LOWER(kota) = LOWER(@Kota)
                            AND LOWER(provinsi) = LOWER(@Provinsi)
                        )
                        OR
                        (kota = '*' AND provinsi = '*')
                    )
                ORDER BY
                    CASE
                        WHEN LOWER(kota) = LOWER(@Kota)
                         AND LOWER(provinsi) = LOWER(@Provinsi)
                        THEN 0
                        ELSE 1
                    END
                LIMIT 1
            ";

            var tarif = await connection.QueryFirstOrDefaultAsync<TarifCheckoutData>(
                sqlTarif,
                new
                {
                    alamatTerpilih.Kota,
                    alamatTerpilih.Provinsi
                }
            );

            if (tarif != null)
            {
                model.Ongkir = tarif.Biaya;
                model.EstimasiPengiriman = tarif.EstimasiHari;
                model.TarifPengirimanTersedia = true;
            }

            await TerapkanPromoAsync(
                connection,
                model,
                model.KodePromo
            );

            return model;
        }

        private async Task TerapkanPromoAsync(
            MySqlConnection connection,
            CheckoutViewModel model,
            string? kodePromo,
            MySqlTransaction? transaction = null)
        {
            model.IdPromo = null;
            model.Diskon = 0;
            model.PesanPromo = null;

            if (string.IsNullOrWhiteSpace(kodePromo))
                return;

            string kode = kodePromo.Trim().ToUpperInvariant();

            var promo = await connection.QueryFirstOrDefaultAsync<PromoCheckoutData>(
                @"
                SELECT
                    id_promo AS IdPromo,
                    kode_promo AS KodePromo,
                    nama_promo AS NamaPromo,
                    tipe AS Tipe,
                    nilai AS Nilai,
                    min_belanja AS MinBelanja
                FROM promo
                WHERE UPPER(kode_promo) = @KodePromo
                    AND status = 'aktif'
                    AND tgl_mulai <= CURDATE()
                    AND tgl_selesai >= CURDATE()
                LIMIT 1
                ",
                new { KodePromo = kode },
                transaction
            );

            if (promo == null)
            {
                model.PesanPromo =
                    "Kode promo tidak ditemukan, tidak aktif, atau sudah kedaluwarsa.";

                return;
            }

            if (model.Subtotal < promo.MinBelanja)
            {
                model.PesanPromo =
                    $"Minimal belanja untuk promo ini adalah Rp {promo.MinBelanja:N0}.";

                return;
            }

            decimal diskon;

            if (promo.Tipe == "persen")
            {
                diskon = Math.Round(
                    model.Subtotal * promo.Nilai / 100,
                    2,
                    MidpointRounding.AwayFromZero
                );
            }
            else if (promo.Tipe == "nominal")
            {
                diskon = promo.Nilai;
            }
            else
            {
                model.PesanPromo = "Tipe promo tidak valid.";
                return;
            }

            diskon = Math.Min(diskon, model.Subtotal);

            model.IdPromo = promo.IdPromo;
            model.Diskon = diskon;
            model.PesanPromo =
                $"Promo {promo.KodePromo} berhasil diterapkan.";
        }

        // GET: /Checkout
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var itemCheckout = AmbilItemCheckout();

            if (itemCheckout.Count == 0)
            {
                TempData["Error"] =
                    "Pilih produk yang ingin dibeli terlebih dahulu.";

                return RedirectToAction("Index", "Keranjang");
            }

            await using var connection = _koneksi.GetConnection();
            await connection.OpenAsync();

            int? idCustomer = await AmbilIdCustomerAsync(connection);

            if (idCustomer == null)
                return RedirectToAction("Index", "User");

            var model = await BangunModelCheckoutAsync(
                connection,
                itemCheckout,
                idCustomer.Value,
                null,
                null,
                null
            );

            if (model.Alamat.Count == 0)
            {
                TempData["Error"] =
                    "Tambahkan alamat pengiriman terlebih dahulu.";

                return RedirectToAction("Tambah", "Alamat");
            }

            if (model.MetodePembayaran.Count == 0)
            {
                TempData["Error"] =
                    "Belum ada metode pembayaran yang tersedia.";

                return RedirectToAction("Index", "Keranjang");
            }

            if (model.Items.Count == 0)
            {
                TempData["Error"] =
                    "Produk yang dipilih sudah tidak tersedia.";

                HapusItemCheckout();

                return RedirectToAction("Index", "Keranjang");
            }

            if (!model.TarifPengirimanTersedia)
            {
                TempData["Error"] =
                    "Tarif pengiriman untuk alamat tersebut belum tersedia.";
            }

            return View(model);
        }

        // POST: /Checkout/Hitung
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Hitung(
            int idAlamat,
            int idMetode,
            string? kodePromo)
        {
            var itemCheckout = AmbilItemCheckout();

            if (itemCheckout.Count == 0)
            {
                TempData["Error"] =
                    "Tidak ada produk yang sedang diproses.";

                return RedirectToAction("Index", "Keranjang");
            }

            await using var connection = _koneksi.GetConnection();
            await connection.OpenAsync();

            int? idCustomer = await AmbilIdCustomerAsync(connection);

            if (idCustomer == null)
                return RedirectToAction("Index", "User");

            var model = await BangunModelCheckoutAsync(
                connection,
                itemCheckout,
                idCustomer.Value,
                idAlamat,
                idMetode,
                kodePromo
            );

            if (!model.Alamat.Any(x => x.IdAlamat == idAlamat))
            {
                TempData["Error"] = "Alamat pengiriman tidak valid.";
                return RedirectToAction(nameof(Index));
            }

            if (!model.TarifPengirimanTersedia)
            {
                model.PesanPromo =
                    string.IsNullOrWhiteSpace(model.PesanPromo)
                        ? "Tarif pengiriman untuk alamat tersebut belum tersedia."
                        : model.PesanPromo + " Tarif pengiriman belum tersedia.";
            }

            return View("Index", model);
        }

        // POST: /Checkout/Proses
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Proses(
            int idAlamat,
            int idMetode,
            string? kodePromo)
        {
            var itemCheckout = AmbilItemCheckout();

            if (itemCheckout.Count == 0)
            {
                TempData["Error"] =
                    "Tidak ada produk yang sedang diproses.";

                return RedirectToAction("Index", "Keranjang");
            }

            string? idUser = User.FindFirstValue(
                ClaimTypes.NameIdentifier
            );

            if (!int.TryParse(idUser, out int userId))
                return RedirectToAction("Login", "Auth");

            await using var connection = _koneksi.GetConnection();
            await connection.OpenAsync();

            await using var transaction =
                await connection.BeginTransactionAsync();

            try
            {
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

                // Pastikan alamat masih aktif saat pesanan dibuat.
                var alamat =
                    await connection.QueryFirstOrDefaultAsync<AlamatCustomer>(
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
                        WHERE id_alamat = @IdAlamat
                            AND id_customer = @IdCustomer
                            AND is_aktif = 1
                        FOR UPDATE
                        ",
                        new
                        {
                            IdAlamat = idAlamat,
                            IdCustomer = idCustomer.Value
                        },
                        transaction
                    );

                if (alamat == null)
                    throw new InvalidOperationException(
                        "Alamat pengiriman tidak valid atau sudah dihapus. Silakan pilih alamat yang masih aktif."
                    );

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

                var produkPesanan =
                    new List<CheckoutItemViewModel>();

                decimal subtotal = 0;

                foreach (var item in itemCheckout)
                {
                    var produk =
                        await connection.QueryFirstOrDefaultAsync<CheckoutItemViewModel>(
                            @"
                            SELECT
                                p.id_produk AS IdProduk,
                                p.nama_produk AS NamaProduk,
                                p.kode_produk AS KodeProduk,
                                p.foto AS Foto,
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
                    {
                        throw new InvalidOperationException(
                            "Salah satu produk sudah tidak tersedia."
                        );
                    }

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

                if (produkPesanan.Count == 0)
                    throw new InvalidOperationException(
                        "Tidak ada produk yang dapat diproses."
                    );

                var tarif =
                    await connection.QueryFirstOrDefaultAsync<TarifCheckoutData>(
                        @"
                        SELECT
                            biaya AS Biaya,
                            estimasi_hari AS EstimasiHari
                        FROM tarif_pengiriman
                        WHERE status = 'aktif'
                            AND (
                                (
                                    LOWER(kota) = LOWER(@Kota)
                                    AND LOWER(provinsi) = LOWER(@Provinsi)
                                )
                                OR
                                (kota = '*' AND provinsi = '*')
                            )
                        ORDER BY
                            CASE
                                WHEN LOWER(kota) = LOWER(@Kota)
                                 AND LOWER(provinsi) = LOWER(@Provinsi)
                                THEN 0
                                ELSE 1
                            END
                        LIMIT 1
                        ",
                        new
                        {
                            alamat.Kota,
                            alamat.Provinsi
                        },
                        transaction
                    );

                if (tarif == null)
                {
                    throw new InvalidOperationException(
                        "Tarif pengiriman untuk alamat ini belum tersedia."
                    );
                }

                decimal ongkir = tarif.Biaya;

                var modelPromo = new CheckoutViewModel
                {
                    Subtotal = subtotal
                };

                await TerapkanPromoAsync(
                    connection,
                    modelPromo,
                    kodePromo,
                    transaction
                );

                decimal diskon = modelPromo.Diskon;
                int? idPromo = modelPromo.IdPromo;

                decimal totalBayar = subtotal + ongkir - diskon;

                if (totalBayar < 0)
                    totalBayar = 0;

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
                        @IdPromo,
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
                        IdCustomer = idCustomer.Value,
                        IdAlamat = idAlamat,
                        IdPromo = idPromo,
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
                    {
                        throw new InvalidOperationException(
                            $"Gagal memperbarui stok {produk.NamaProduk}."
                        );
                    }
                }

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

                await transaction.CommitAsync();

                HapusProdukYangDibeli(itemCheckout);
                HapusItemCheckout();

                TempData["Success"] =
                    "Pesanan berhasil dibuat. Silakan lanjutkan pembayaran.";

                return RedirectToAction(
                    nameof(Selesai),
                    new { id = idPemesanan }
                );
            }
            catch (Exception ex)
            {
                try
                {
                    await transaction.RollbackAsync();
                }
                catch
                {
                    // Transaksi mungkin sudah dibatalkan.
                }

                TempData["Error"] =
                    ex is MySqlException
                        ? "Terjadi kesalahan database saat membuat pesanan. Periksa kembali data dan konfigurasi database."
                        : ex.Message;

                return RedirectToAction(nameof(Index));
            }
        }

        // GET: /Checkout/Selesai/1
        [HttpGet]
        public async Task<IActionResult> Selesai(int id)
        {
            await using var connection = _koneksi.GetConnection();
            await connection.OpenAsync();

            int? idCustomer = await AmbilIdCustomerAsync(connection);

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
                        IdCustomer = idCustomer.Value
                    }
                );

            if (pesanan == null)
                return NotFound();

            return View(pesanan);
        }

        private class TarifCheckoutData
        {
            public decimal Biaya { get; set; }

            public string? EstimasiHari { get; set; }
        }

        private class PromoCheckoutData
        {
            public int IdPromo { get; set; }

            public string KodePromo { get; set; } = "";

            public string NamaPromo { get; set; } = "";

            public string Tipe { get; set; } = "";

            public decimal Nilai { get; set; }

            public decimal MinBelanja { get; set; }
        }
    }
}
