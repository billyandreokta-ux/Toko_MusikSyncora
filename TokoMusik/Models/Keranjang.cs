namespace TokoMusik.Models
{
    public class ItemKeranjang
    {
        public int IdProduk { get; set; }
        public string Kode { get; set; } = "";
        public string Nama { get; set; } = "";
        public decimal Harga { get; set; }
        public int Jumlah { get; set; }
    }

    // keranjang disimpan di memori selama user login
    public static class Keranjang
    {
        public static List<ItemKeranjang> Items { get; } = new();

        public static void Tambah(ItemKeranjang item)
        {
            var ada = Items.FirstOrDefault(i => i.IdProduk == item.IdProduk);
            if (ada != null) ada.Jumlah += item.Jumlah;
            else Items.Add(item);
        }

        public static decimal Subtotal() => Items.Sum(i => i.Harga * i.Jumlah);

        public static void Kosongkan() => Items.Clear();
    }
}