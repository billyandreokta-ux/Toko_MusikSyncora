namespace TokoMusik.Web.Models
{
    public class InventoryViewModel
    {
        public string? KataKunci { get; set; }
        public string StatusFilter { get; set; } = "semua";

        public int TotalProduk { get; set; }
        public int TotalStok { get; set; }
        public int StokHabis { get; set; }
        public int StokMenipis { get; set; }

        public List<InventoryItemViewModel> Produk { get; set; } = new();
    }

    public class InventoryItemViewModel
    {
        public int IdProduk { get; set; }
        public string KodeProduk { get; set; } = "";
        public string NamaProduk { get; set; } = "";
        public string NamaCategory { get; set; } = "";
        public string NamaMerk { get; set; } = "";
        public decimal HargaJual { get; set; }
        public string? Foto { get; set; }
        public string StatusProduk { get; set; } = "aktif";
        public int JumlahStok { get; set; }
        public int StokMinimum { get; set; }

        public string StatusStok
        {
            get
            {
                if (JumlahStok <= 0)
                    return "Habis";

                if (JumlahStok <= StokMinimum)
                    return "Menipis";

                return "Aman";
            }
        }
    }

    public class InventoryDetailViewModel
    {
        public int IdProduk { get; set; }
        public string KodeProduk { get; set; } = "";
        public string NamaProduk { get; set; } = "";
        public string NamaCategory { get; set; } = "";
        public string NamaMerk { get; set; } = "";
        public decimal HargaJual { get; set; }
        public string? Deskripsi { get; set; }
        public string? Foto { get; set; }
        public string StatusProduk { get; set; } = "aktif";
        public int JumlahStok { get; set; }
        public int StokMinimum { get; set; }
        public DateTime? UpdatedAt { get; set; }

        public string StatusStok
        {
            get
            {
                if (JumlahStok <= 0)
                    return "Habis";

                if (JumlahStok <= StokMinimum)
                    return "Menipis";

                return "Aman";
            }
        }
    }
}