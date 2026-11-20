
using System.Text.Json;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TokoMusik.Web.Data;
using TokoMusik.Web.Models;

namespace TokoMusik.Web.Controllers
{
    [Authorize(Roles = "user")]
    public class KeranjangController : Controller
    {
        private readonly Koneksi _koneksi;
        private const string SessionKey = "TOKOMUSIK_KERANJANG";

        public KeranjangController(Koneksi koneksi)
        {
            _koneksi = koneksi;
        }

        private List<KeranjangItem> AmbilKeranjang()
        {
            var json = HttpContext.Session.GetString(SessionKey);

            if (string.IsNullOrEmpty(json))
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

        // GET: /Keranjang
        public async Task<IActionResult> Index()
        {
            var keranjang = AmbilKeranjang();

            if (keranjang.Count == 0)
                return View(new List<KeranjangViewModel>());

            using var connection = _koneksi.GetConnection();
            await connection.OpenAsync();

            var hasil = new List<KeranjangViewModel>();
            var keranjangValid = new List<KeranjangItem>();

            foreach (var item in keranjang)
            {
                var produk = await connection.QueryFirstOrDefaultAsync<KeranjangViewModel>(
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

                if (produk == null)
                    continue;

                if (produk.Stok <= 0)
                    continue;

                produk.Jumlah = Math.Min(item.Jumlah, produk.Stok);

                if (produk.Jumlah <= 0)
                    continue;

                hasil.Add(produk);

                keranjangValid.Add(new KeranjangItem
                {
                    IdProduk = item.IdProduk,
                    Jumlah = produk.Jumlah
                });
            }

            SimpanKeranjang(keranjangValid);

            return View(hasil);
        }

        // POST: /Keranjang/Tambah
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Tambah(int idProduk, int jumlah = 1)
        {
            if (jumlah < 1)
                jumlah = 1;

            using var connection = _koneksi.GetConnection();
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
                return RedirectToAction("Detail", "Produk", new { id = idProduk });
            }

            var keranjang = AmbilKeranjang();
            var item = keranjang.FirstOrDefault(x => x.IdProduk == idProduk);

            int jumlahBaru = (item?.Jumlah ?? 0) + jumlah;

            if (jumlahBaru > stok)
            {
                TempData["Error"] = $"Stok tersedia hanya {stok} unit.";
                return RedirectToAction("Index");
            }

            if (item == null)
            {
                keranjang.Add(new KeranjangItem
                {
                    IdProduk = idProduk,
                    Jumlah = jumlah
                });
            }
            else
            {
                item.Jumlah = jumlahBaru;
            }

            SimpanKeranjang(keranjang);

            TempData["Success"] = "Produk berhasil ditambahkan ke keranjang.";

            return RedirectToAction("Index");
        }

        // POST: /Keranjang/UbahJumlah
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UbahJumlah(int idProduk, int jumlah)
        {
            var keranjang = AmbilKeranjang();
            var item = keranjang.FirstOrDefault(x => x.IdProduk == idProduk);

            if (item == null)
                return RedirectToAction("Index");

            if (jumlah <= 0)
            {
                keranjang.Remove(item);
                SimpanKeranjang(keranjang);
                return RedirectToAction("Index");
            }

            using var connection = _koneksi.GetConnection();
            await connection.OpenAsync();

            int stok = await connection.ExecuteScalarAsync<int>(
                @"
                SELECT COALESCE(s.jumlah, 0)
                FROM produk p
                LEFT JOIN stock s ON s.id_produk = p.id_produk
                WHERE p.id_produk = @IdProduk
                  AND p.status = 'aktif'
                ",
                new { IdProduk = idProduk }
            );

            if (stok <= 0)
            {
                keranjang.Remove(item);
                SimpanKeranjang(keranjang);

                TempData["Error"] = "Produk sudah tidak tersedia.";
                return RedirectToAction("Index");
            }

            if (jumlah > stok)
            {
                TempData["Error"] = $"Stok tersedia hanya {stok} unit.";
                return RedirectToAction("Index");
            }

            item.Jumlah = jumlah;
            SimpanKeranjang(keranjang);

            TempData["Success"] = "Jumlah produk berhasil diperbarui.";

            return RedirectToAction("Index");
        }

        // POST: /Keranjang/Hapus
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Hapus(int idProduk)
        {
            var keranjang = AmbilKeranjang();

            keranjang.RemoveAll(x => x.IdProduk == idProduk);

            SimpanKeranjang(keranjang);

            TempData["Success"] = "Produk berhasil dihapus dari keranjang.";

            return RedirectToAction("Index");
        }

        // POST: /Keranjang/Kosongkan
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Kosongkan()
        {
            SimpanKeranjang(new List<KeranjangItem>());

            TempData["Success"] = "Keranjang berhasil dikosongkan.";

            return RedirectToAction("Index");
        }
    }
}
