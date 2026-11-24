
using System;
using System.Collections.Generic;

namespace TokoMusik.Web.Models
{
    public class AdminPesananGabunganViewModel
    {
        public int IdPemesanan { get; set; }
        public string NamaCustomer { get; set; } = "";
        public string Username { get; set; } = "";
        public DateTime TglPesan { get; set; }
        public decimal TotalBayar { get; set; }
        public string Status { get; set; } = "";
        public string StatusPembayaran { get; set; } = "";

        public int? IdPayment { get; set; }
        public string? NamaMetode { get; set; }
        public DateTime? TglBayar { get; set; }
        public decimal? JumlahBayar { get; set; }
        public string? BuktiBayar { get; set; }
    }

    public class AdminPesananGabunganDetailViewModel
    {
        public int IdPemesanan { get; set; }
        public string NamaCustomer { get; set; } = "";
        public string Username { get; set; } = "";
        public string? Telepon { get; set; }

        public DateTime TglPesan { get; set; }
        public decimal Subtotal { get; set; }
        public decimal Diskon { get; set; }
        public decimal Ongkir { get; set; }
        public decimal TotalBayar { get; set; }
        public string Status { get; set; } = "";

        public int? IdPayment { get; set; }
        public string StatusPembayaran { get; set; } = "belum_bayar";
        public string? NamaMetode { get; set; }
        public DateTime? TglBayar { get; set; }
        public decimal? JumlahBayar { get; set; }
        public string? BuktiBayar { get; set; }

        public string NamaPenerima { get; set; } = "-";
        public string TeleponPenerima { get; set; } = "-";
        public string AlamatLengkap { get; set; } = "-";
        public string Kota { get; set; } = "-";
        public string Provinsi { get; set; } = "-";
        public string KodePos { get; set; } = "-";

        public string? Kurir { get; set; }
        public string? NoResi { get; set; }
        public string? StatusPengiriman { get; set; }

        public List<AdminPesananGabunganItemViewModel> Items { get; set; } = new();
    }

    public class AdminPesananGabunganItemViewModel
    {
        public string NamaProduk { get; set; } = "";
        public string KodeProduk { get; set; } = "";
        public int Jumlah { get; set; }
        public decimal HargaSatuan { get; set; }
        public decimal Subtotal { get; set; }
    }
}
