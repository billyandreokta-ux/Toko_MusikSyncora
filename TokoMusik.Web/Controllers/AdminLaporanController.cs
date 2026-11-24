
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TokoMusik.Web.Data;
using TokoMusik.Web.Models;

namespace TokoMusik.Web.Controllers
{
    [Authorize(Roles = "admin")]
    public class AdminLaporanController : Controller
    {
        private readonly Koneksi _koneksi;

        public AdminLaporanController(Koneksi koneksi)
        {
            _koneksi = koneksi;
        }

        public async Task<IActionResult> Index(DateTime? tanggalMulai, DateTime? tanggalSelesai)
        {
            var hariIni = DateTime.Today;

            var mulai = tanggalMulai?.Date ?? new DateTime(hariIni.Year, hariIni.Month, 1);
            var selesai = tanggalSelesai?.Date ?? hariIni;

            if (mulai > selesai)
            {
                ModelState.AddModelError(
                    "",
                    "Tanggal mulai tidak boleh melewati tanggal selesai."
                );

                var sementara = mulai;
                mulai = selesai;
                selesai = sementara;
            }

            // Batas akhir dibuat eksklusif agar seluruh transaksi
            // pada tanggal selesai tetap masuk ke laporan.
            var sampaiEksklusif = selesai.AddDays(1);

            var model = new LaporanViewModel
            {
                TanggalMulai = mulai,
                TanggalSelesai = selesai
            };

            using var connection = _koneksi.GetConnection();
            await connection.OpenAsync();

            // =====================================================
            // 1. RINGKASAN PENJUALAN
            // =====================================================

            const string sqlRingkasanPenjualan = @"
                SELECT
                    COUNT(*) AS TotalPesanan,
                    COALESCE(SUM(p.total_bayar), 0) AS TotalPenjualan
                FROM pemesanan p
                WHERE p.tgl_pesan >= @Mulai
                  AND p.tgl_pesan < @Sampai
                  AND p.status IN ('dibayar', 'diproses', 'dikirim', 'selesai');";

            var ringkasanPenjualan = await connection.QueryFirstAsync(
                sqlRingkasanPenjualan,
                new
                {
                    Mulai = mulai,
                    Sampai = sampaiEksklusif
                }
            );

            model.TotalPesanan = Convert.ToInt32(ringkasanPenjualan.TotalPesanan);
            model.TotalPenjualan = Convert.ToDecimal(ringkasanPenjualan.TotalPenjualan);

            // =====================================================
            // 2. TOTAL PRODUK TERJUAL
            // =====================================================

            const string sqlTotalProdukTerjual = @"
                SELECT COALESCE(SUM(dp.jumlah), 0)
                FROM detail_pesanan dp
                INNER JOIN pemesanan p
                    ON p.id_pemesanan = dp.id_pemesanan
                WHERE p.tgl_pesan >= @Mulai
                  AND p.tgl_pesan < @Sampai
                  AND p.status IN ('dibayar', 'diproses', 'dikirim', 'selesai');";

            var totalProdukTerjual = await connection.ExecuteScalarAsync<long>(
                sqlTotalProdukTerjual,
                new
                {
                    Mulai = mulai,
                    Sampai = sampaiEksklusif
                }
            );

            model.TotalProdukTerjual = Convert.ToInt32(totalProdukTerjual);

            // =====================================================
            // 3. DAFTAR PENJUALAN
            // =====================================================

            const string sqlDaftarPenjualan = @"
                SELECT
                    p.id_pemesanan AS IdPemesanan,
                    p.tgl_pesan AS Tanggal,
                    COALESCE(c.nama_customer, 'Customer') AS NamaCustomer,
                    p.total_bayar AS TotalBayar,
                    p.status AS Status
                FROM pemesanan p
                LEFT JOIN customer c
                    ON c.id_customer = p.id_customer
                WHERE p.tgl_pesan >= @Mulai
                  AND p.tgl_pesan < @Sampai
                  AND p.status IN ('dibayar', 'diproses', 'dikirim', 'selesai')
                ORDER BY p.tgl_pesan DESC
                LIMIT 500;";

            model.DaftarPenjualan = (
                await connection.QueryAsync<LaporanPenjualanItemViewModel>(
                    sqlDaftarPenjualan,
                    new
                    {
                        Mulai = mulai,
                        Sampai = sampaiEksklusif
                    }
                )
            ).ToList();

            // =====================================================
            // 4. PRODUK TERLARIS
            // =====================================================

            const string sqlProdukTerlaris = @"
                SELECT
                    pr.kode_produk AS KodeProduk,
                    pr.nama_produk AS NamaProduk,
                    SUM(dp.jumlah) AS JumlahTerjual,
                    SUM(dp.subtotal) AS TotalPenjualan
                FROM detail_pesanan dp
                INNER JOIN pemesanan p
                    ON p.id_pemesanan = dp.id_pemesanan
                INNER JOIN produk pr
                    ON pr.id_produk = dp.id_produk
                WHERE p.tgl_pesan >= @Mulai
                  AND p.tgl_pesan < @Sampai
                  AND p.status IN ('dibayar', 'diproses', 'dikirim', 'selesai')
                GROUP BY
                    pr.id_produk,
                    pr.kode_produk,
                    pr.nama_produk
                ORDER BY JumlahTerjual DESC
                LIMIT 10;";

            model.ProdukTerlaris = (
                await connection.QueryAsync<LaporanProdukTerlarisViewModel>(
                    sqlProdukTerlaris,
                    new
                    {
                        Mulai = mulai,
                        Sampai = sampaiEksklusif
                    }
                )
            ).ToList();

            // =====================================================
            // 5. RINGKASAN PEMBAYARAN
            // =====================================================

            const string sqlRingkasanPembayaran = @"
                SELECT
                    COUNT(CASE WHEN status = 'berhasil' THEN 1 END) AS Berhasil,
                    COUNT(CASE WHEN status = 'pending' THEN 1 END) AS Pending,
                    COUNT(CASE WHEN status = 'gagal' THEN 1 END) AS Gagal,
                    COALESCE(
                        SUM(
                            CASE
                                WHEN status = 'berhasil' THEN jumlah_bayar
                                ELSE 0
                            END
                        ),
                        0
                    ) AS TotalBerhasil
                FROM payments
                WHERE tgl_bayar >= @Mulai
                  AND tgl_bayar < @Sampai;";

            var ringkasanPembayaran = await connection.QueryFirstAsync(
                sqlRingkasanPembayaran,
                new
                {
                    Mulai = mulai,
                    Sampai = sampaiEksklusif
                }
            );

            model.JumlahPembayaranBerhasil =
                Convert.ToInt32(ringkasanPembayaran.Berhasil);

            model.JumlahPembayaranPending =
                Convert.ToInt32(ringkasanPembayaran.Pending);

            model.JumlahPembayaranGagal =
                Convert.ToInt32(ringkasanPembayaran.Gagal);

            model.TotalPembayaranBerhasil =
                Convert.ToDecimal(ringkasanPembayaran.TotalBerhasil);

            // =====================================================
            // 6. RIWAYAT PEMBAYARAN
            // =====================================================

            const string sqlDaftarPembayaran = @"
                SELECT
                    id_payment AS IdPayment,
                    id_pemesanan AS IdPemesanan,
                    id_metode AS IdMetode,
                    tgl_bayar AS TanggalBayar,
                    jumlah_bayar AS JumlahBayar,
                    bukti_bayar AS BuktiBayar,
                    status AS Status
                FROM payments
                WHERE tgl_bayar >= @Mulai
                  AND tgl_bayar < @Sampai
                ORDER BY tgl_bayar DESC
                LIMIT 500;";

            model.DaftarPembayaran = (
                await connection.QueryAsync<LaporanPembayaranItemViewModel>(
                    sqlDaftarPembayaran,
                    new
                    {
                        Mulai = mulai,
                        Sampai = sampaiEksklusif
                    }
                )
            ).ToList();

            // =====================================================
            // 7. RINGKASAN PEMBELIAN STOK
            // =====================================================

            const string sqlRingkasanPembelian = @"
                SELECT
                    COUNT(*) AS JumlahTransaksi,
                    COALESCE(SUM(jumlah), 0) AS TotalUnit,
                    COALESCE(SUM(total), 0) AS TotalNilai
                FROM purchase_stock
                WHERE tanggal >= @Mulai
                  AND tanggal < @Sampai;";

            var ringkasanPembelian = await connection.QueryFirstAsync(
                sqlRingkasanPembelian,
                new
                {
                    Mulai = mulai,
                    Sampai = sampaiEksklusif
                }
            );

            model.JumlahTransaksiPembelian =
                Convert.ToInt32(ringkasanPembelian.JumlahTransaksi);

            model.TotalUnitDibeli =
                Convert.ToInt32(ringkasanPembelian.TotalUnit);

            model.TotalNilaiPembelian =
                Convert.ToDecimal(ringkasanPembelian.TotalNilai);

            // =====================================================
            // 8. DAFTAR PEMBELIAN STOK
            // =====================================================

            const string sqlDaftarPembelian = @"
                SELECT
                    ps.id_purchase AS IdPurchase,
                    ps.tanggal AS Tanggal,
                    COALESCE(s.nama_supplier, 'Supplier') AS NamaSupplier,
                    COALESCE(pr.nama_produk, 'Produk') AS NamaProduk,
                    ps.jumlah AS Jumlah,
                    ps.harga_beli AS HargaBeli,
                    ps.total AS Total
                FROM purchase_stock ps
                LEFT JOIN supplier s
                    ON s.id_supplier = ps.id_supplier
                LEFT JOIN produk pr
                    ON pr.id_produk = ps.id_produk
                WHERE ps.tanggal >= @Mulai
                  AND ps.tanggal < @Sampai
                ORDER BY ps.tanggal DESC, ps.id_purchase DESC
                LIMIT 500;";

            model.DaftarPembelian = (
                await connection.QueryAsync<LaporanPembelianItemViewModel>(
                    sqlDaftarPembelian,
                    new
                    {
                        Mulai = mulai,
                        Sampai = sampaiEksklusif
                    }
                )
            ).ToList();

            // =====================================================
            // 9. PEMBELIAN BERDASARKAN SUPPLIER
            // =====================================================

            const string sqlPembelianSupplier = @"
                SELECT
                    COALESCE(
                        s.nama_supplier,
                        'Supplier tidak diketahui'
                    ) AS NamaSupplier,
                    COUNT(ps.id_purchase) AS JumlahTransaksi,
                    COALESCE(SUM(ps.jumlah), 0) AS TotalUnit,
                    COALESCE(SUM(ps.total), 0) AS TotalPembelian
                FROM purchase_stock ps
                LEFT JOIN supplier s
                    ON s.id_supplier = ps.id_supplier
                WHERE ps.tanggal >= @Mulai
                  AND ps.tanggal < @Sampai
                GROUP BY
                    s.id_supplier,
                    s.nama_supplier
                ORDER BY TotalPembelian DESC;";

            model.PembelianPerSupplier = (
                await connection.QueryAsync<LaporanSupplierViewModel>(
                    sqlPembelianSupplier,
                    new
                    {
                        Mulai = mulai,
                        Sampai = sampaiEksklusif
                    }
                )
            ).ToList();

            // =====================================================
            // 10. RINGKASAN PENGELUARAN
            // =====================================================

            const string sqlRingkasanPengeluaran = @"
                SELECT
                    COUNT(*) AS JumlahTransaksi,
                    COALESCE(SUM(jumlah), 0) AS TotalPengeluaran
                FROM pengeluaran
                WHERE tanggal >= @Mulai
                  AND tanggal < @Sampai;";

            var ringkasanPengeluaran = await connection.QueryFirstAsync(
                sqlRingkasanPengeluaran,
                new
                {
                    Mulai = mulai,
                    Sampai = sampaiEksklusif
                }
            );

            model.JumlahTransaksiPengeluaran =
                Convert.ToInt32(ringkasanPengeluaran.JumlahTransaksi);

            model.TotalPengeluaran =
                Convert.ToDecimal(ringkasanPengeluaran.TotalPengeluaran);

            // =====================================================
            // 11. DAFTAR PENGELUARAN
            // =====================================================

            const string sqlDaftarPengeluaran = @"
                SELECT
                    id_pengeluaran AS IdPengeluaran,
                    tanggal AS Tanggal,
                    COALESCE(kategori, 'Tanpa kategori') AS Kategori,
                    jumlah AS Jumlah,
                    keterangan AS Keterangan
                FROM pengeluaran
                WHERE tanggal >= @Mulai
                  AND tanggal < @Sampai
                ORDER BY tanggal DESC, id_pengeluaran DESC
                LIMIT 500;";

            model.DaftarPengeluaran = (
                await connection.QueryAsync<LaporanPengeluaranItemViewModel>(
                    sqlDaftarPengeluaran,
                    new
                    {
                        Mulai = mulai,
                        Sampai = sampaiEksklusif
                    }
                )
            ).ToList();

            // =====================================================
            // 12. PENGELUARAN BERDASARKAN KATEGORI
            // =====================================================

            const string sqlPengeluaranKategori = @"
                SELECT
                    COALESCE(kategori, 'Tanpa kategori') AS Kategori,
                    COUNT(*) AS JumlahTransaksi,
                    COALESCE(SUM(jumlah), 0) AS TotalPengeluaran
                FROM pengeluaran
                WHERE tanggal >= @Mulai
                  AND tanggal < @Sampai
                GROUP BY kategori
                ORDER BY TotalPengeluaran DESC;";

            model.PengeluaranPerKategori = (
                await connection.QueryAsync<LaporanKategoriPengeluaranViewModel>(
                    sqlPengeluaranKategori,
                    new
                    {
                        Mulai = mulai,
                        Sampai = sampaiEksklusif
                    }
                )
            ).ToList();

            // =====================================================
            // SELESAI
            // =====================================================

            return View(model);
        }
    }
}
