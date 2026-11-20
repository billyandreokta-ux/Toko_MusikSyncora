
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TokoMusik.Web.Data;
using TokoMusik.Web.Models;

namespace TokoMusik.Web.Controllers
{
    [Authorize(Roles = "admin")]
    public class AdminLaporanInventarisController : Controller
    {
        private readonly Koneksi _koneksi;

        public AdminLaporanInventarisController(Koneksi koneksi)
        {
            _koneksi = koneksi;
        }

        [HttpGet]
        public async Task<IActionResult> Index(
            DateTime? tanggalMulai,
            DateTime? tanggalSelesai)
        {
            var hariIni = DateTime.Today;

            var mulai = tanggalMulai?.Date
                ?? new DateTime(hariIni.Year, hariIni.Month, 1);

            var selesai = tanggalSelesai?.Date
                ?? hariIni;

            if (mulai > selesai)
            {
                TempData["Gagal"] =
                    "Tanggal mulai tidak boleh melebihi tanggal selesai.";

                mulai = new DateTime(hariIni.Year, hariIni.Month, 1);
                selesai = hariIni;
            }

            var model = new LaporanInventarisViewModel
            {
                TanggalMulai = mulai,
                TanggalSelesai = selesai
            };

            using var connection = _koneksi.GetConnection();
            await connection.OpenAsync();

            var parameter = new
            {
                TanggalMulai = mulai,
                BatasTanggal = selesai.AddDays(1)
            };

            // =========================
            // RINGKASAN STOK MASUK
            // =========================

            var masuk = await connection.QuerySingleAsync<dynamic>(
                @"SELECT
                      COUNT(*) AS TotalTransaksi,
                      COALESCE(SUM(jumlah), 0) AS TotalUnit,
                      COALESCE(SUM(total), 0) AS TotalPembelian,
                      COUNT(DISTINCT id_produk) AS JumlahProduk,
                      COUNT(DISTINCT id_supplier) AS JumlahSupplier
                  FROM purchase_stock
                  WHERE tanggal >= @TanggalMulai
                    AND tanggal < @BatasTanggal",
                parameter);

            model.TotalTransaksiMasuk = Convert.ToInt32(masuk.TotalTransaksi);
            model.TotalUnitMasuk = Convert.ToInt32(masuk.TotalUnit);
            model.TotalNilaiPembelian = Convert.ToDecimal(masuk.TotalPembelian);
            model.JumlahProdukDirestock = Convert.ToInt32(masuk.JumlahProduk);
            model.JumlahSupplierAktifTransaksi = Convert.ToInt32(masuk.JumlahSupplier);

            // =========================
            // RINGKASAN STOK KELUAR
            // =========================

            var keluar = await connection.QuerySingleAsync<dynamic>(
                @"SELECT
                      COUNT(*) AS TotalTransaksi,
                      COALESCE(SUM(jumlah), 0) AS TotalUnit
                  FROM stock_mutation
                  WHERE jenis = 'keluar'
                    AND tanggal >= @TanggalMulai
                    AND tanggal < @BatasTanggal",
                parameter);

            model.TotalTransaksiKeluar = Convert.ToInt32(keluar.TotalTransaksi);
            model.TotalUnitKeluar = Convert.ToInt32(keluar.TotalUnit);

            // =========================
            // PRODUK PALING SERING DIRESTOCK
            // =========================

            var produk = await connection.QueryAsync<LaporanProdukRestockViewModel>(
                @"SELECT
                      p.id_produk AS IdProduk,
                      p.kode_produk AS KodeProduk,
                      p.nama_produk AS NamaProduk,
                      COALESCE(SUM(ps.jumlah), 0) AS TotalUnitMasuk,
                      COUNT(ps.id_purchase) AS FrekuensiRestock,
                      COALESCE(SUM(ps.total), 0) AS TotalPembelian
                  FROM purchase_stock ps
                  INNER JOIN produk p
                      ON p.id_produk = ps.id_produk
                  WHERE ps.tanggal >= @TanggalMulai
                    AND ps.tanggal < @BatasTanggal
                  GROUP BY
                      p.id_produk,
                      p.kode_produk,
                      p.nama_produk
                  ORDER BY
                      TotalUnitMasuk DESC,
                      TotalPembelian DESC
                  LIMIT 10",
                parameter);

            model.ProdukTerlarisRestock = produk.ToList();

            // =========================
            // PEMBELIAN PER SUPPLIER
            // =========================

            var supplier = await connection.QueryAsync<LaporanSupplierViewModel>(
                @"SELECT
                      s.id_supplier AS IdSupplier,
                      s.nama_supplier AS NamaSupplier,
                      COUNT(ps.id_purchase) AS TotalTransaksi,
                      COALESCE(SUM(ps.jumlah), 0) AS TotalUnit,
                      COALESCE(SUM(ps.total), 0) AS TotalPembelian
                  FROM purchase_stock ps
                  INNER JOIN supplier s
                      ON s.id_supplier = ps.id_supplier
                  WHERE ps.tanggal >= @TanggalMulai
                    AND ps.tanggal < @BatasTanggal
                  GROUP BY
                      s.id_supplier,
                      s.nama_supplier
                  ORDER BY
                      TotalPembelian DESC
                  LIMIT 10",
                parameter);

            model.PembelianPerSupplier = supplier.ToList();

            // =========================
            // PERGERAKAN STOK
            // =========================

            model.PergerakanStok = new List<LaporanPergerakanStokViewModel>
            {
                new LaporanPergerakanStokViewModel
                {
                    JenisTransaksi = "Stok Masuk",
                    TotalTransaksi = model.TotalTransaksiMasuk,
                    TotalUnit = model.TotalUnitMasuk
                },
                new LaporanPergerakanStokViewModel
                {
                    JenisTransaksi = "Stok Keluar",
                    TotalTransaksi = model.TotalTransaksiKeluar,
                    TotalUnit = model.TotalUnitKeluar
                }
            };

            return View(model);
        }
    }
}
