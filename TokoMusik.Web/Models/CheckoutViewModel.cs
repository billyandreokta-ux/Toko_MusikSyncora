
using System.ComponentModel.DataAnnotations;

namespace TokoMusik.Web.Models
{
    public class CheckoutViewModel
    {
        public List<CheckoutItemViewModel> Items { get; set; } = new();

        public List<AlamatCustomer> Alamat { get; set; } = new();

        public List<MetodePembayaranViewModel> MetodePembayaran { get; set; } = new();

        public int IdAlamat { get; set; }

        public int IdMetode { get; set; }

        public string? KodePromo { get; set; }

        public int? IdPromo { get; set; }

        public decimal Subtotal { get; set; }

        public decimal Ongkir { get; set; }

        public decimal Diskon { get; set; }

        public decimal TotalBayar => Subtotal + Ongkir - Diskon;

        public string? PesanPromo { get; set; }

        public bool TarifPengirimanTersedia { get; set; }

        public string? EstimasiPengiriman { get; set; }
    }

    public class CheckoutItemViewModel
    {
        public int IdProduk { get; set; }

        public string NamaProduk { get; set; } = "";

        public string KodeProduk { get; set; } = "";

        public string? Foto { get; set; }

        public decimal HargaJual { get; set; }

        public int Jumlah { get; set; }

        public int Stok { get; set; }

        public decimal Subtotal => HargaJual * Jumlah;
    }

    public class MetodePembayaranViewModel
    {
        public int IdMetode { get; set; }

        public string NamaMetode { get; set; } = "";

        public string? Keterangan { get; set; }
    }

    public class CheckoutSelesaiViewModel
    {
        public int IdPemesanan { get; set; }

        public DateTime TanggalPesan { get; set; }

        public string StatusPesanan { get; set; } = "";

        public string NamaMetode { get; set; } = "";

        public decimal Subtotal { get; set; }

        public decimal Ongkir { get; set; }

        public decimal Diskon { get; set; }

        public decimal TotalBayar { get; set; }

        public string NamaPenerima { get; set; } = "";

        public string Telepon { get; set; } = "";

        public string AlamatLengkap { get; set; } = "";

        public string Kota { get; set; } = "";

        public string Provinsi { get; set; } = "";

        public string KodePos { get; set; } = "";
    }
}
