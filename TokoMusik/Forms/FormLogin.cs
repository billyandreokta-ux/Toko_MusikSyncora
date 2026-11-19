using TokoMusik.Data;

namespace TokoMusik.Forms
{
    public class FormLogin : Form
    {
        private readonly TextBox txtUsername = new();
        private readonly TextBox txtPassword = new();
        private readonly Button btnLogin = new();
        private readonly Label lblPesan = new();

        public FormLogin()
        {
            Text = "Login - Toko Alat Musik";
            ClientSize = new Size(360, 310);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            Font = new Font("Segoe UI", 10F);

            var lblJudul = new Label
            {
                Text = "TOKO ALAT MUSIK",
                Font = new Font("Segoe UI", 16F, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter,
                Bounds = new Rectangle(0, 20, 360, 40)
            };
            var lblUser = new Label { Text = "Username", Bounds = new Rectangle(40, 80, 280, 22) };
            txtUsername.Bounds = new Rectangle(40, 104, 280, 28);
            txtUsername.MaxLength = 50;   // sama dengan kolom username di database
            var lblPass = new Label { Text = "Password", Bounds = new Rectangle(40, 145, 280, 22) };
            txtPassword.Bounds = new Rectangle(40, 169, 280, 28);
            txtPassword.UseSystemPasswordChar = true;
            txtPassword.MaxLength = 72;   // batas panjang password untuk BCrypt

            btnLogin.Text = "Login";
            btnLogin.Bounds = new Rectangle(40, 215, 280, 36);
            btnLogin.Click += BtnLogin_Click;

            lblPesan.ForeColor = Color.Firebrick;
            lblPesan.Bounds = new Rectangle(40, 260, 280, 40);

            Controls.AddRange(new Control[]
            {
                lblJudul, lblUser, txtUsername, lblPass, txtPassword, btnLogin, lblPesan
            });
            AcceptButton = btnLogin;
        }

        private void BtnLogin_Click(object? sender, EventArgs e)
        {
            lblPesan.Text = "";

            if (string.IsNullOrWhiteSpace(txtUsername.Text) || string.IsNullOrEmpty(txtPassword.Text))
            {
                lblPesan.Text = "Username dan password wajib diisi.";
                return;
            }

            if (AuthService.Login(txtUsername.Text.Trim(), txtPassword.Text, out string pesan))
            {
                DialogResult = DialogResult.OK;
                Close();
            }
            else
            {
                lblPesan.Text = pesan;
                txtPassword.Clear();      // kosongkan password setelah gagal
                txtPassword.Focus();
            }
        }
    }
}