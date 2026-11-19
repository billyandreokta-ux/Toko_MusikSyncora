namespace TokoMusik.Models
{
    public static class Sesi
    {
        public static int IdUser { get; set; }
        public static string Username { get; set; } = "";
        public static string Role { get; set; } = "";

        public static void Kosongkan()
        {
            IdUser = 0;
            Username = "";
            Role = "";
        }
    }
}