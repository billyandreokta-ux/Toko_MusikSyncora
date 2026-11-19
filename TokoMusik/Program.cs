using TokoMusik.Data;
using TokoMusik.Forms;
using TokoMusik.Models;

namespace TokoMusik
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            ApplicationConfiguration.Initialize();

            try
            {
                AuthService.SiapkanAkunAwal();
            }
            catch (Exception ex)
            {
                // detail error hanya ke jendela Output Visual Studio
                System.Diagnostics.Debug.WriteLine(ex);
                MessageBox.Show(
                    "Tidak dapat terhubung ke database.\nPastikan MySQL sudah berjalan, lalu buka aplikasi lagi.",
                    "Toko Alat Musik", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            while (true)
            {
                using var login = new FormLogin();
                if (login.ShowDialog() != DialogResult.OK)
                    break;

                using Form dashboard = Sesi.Role == "admin" ? new FormAdmin() : new FormUser();
                dashboard.ShowDialog();
                Sesi.Kosongkan();
            }
        }
    }
}