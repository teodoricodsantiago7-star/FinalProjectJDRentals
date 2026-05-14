using System;
using System.Data.SqlClient;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Drawing;

namespace FinalProject
{
    public partial class ForgotPassword : Form
    {
        [DllImport("user32.dll")]
        public static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);
        [DllImport("user32.dll")]
        public static extern bool ReleaseCapture();

        private void ForgotPassword_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                ReleaseCapture();
                SendMessage(Handle, 0xA1, 0x2, 0);
            }
        }

        private string generatedCode;
        private string userEmail;

        public ForgotPassword()
        {
            InitializeComponent();
        }

        private void btnSubmit_Click_1(object sender, EventArgs e)
        {
            string email = txtEmail.Text.Trim();

            if (string.IsNullOrWhiteSpace(email) || email == "Email@gmail.com")
            {
                MessageBox.Show("Please enter your email address.");
                return;
            }

            string emailPattern = @"^[^@\s]+@[^@\s]+\.[^@\s]+$";
            if (!Regex.IsMatch(email, emailPattern))
            {
                MessageBox.Show("Please enter a valid email address (e.g., name@gmail.com).");
                return;
            }

            if (!EmailExists(email))
            {
                MessageBox.Show("No account found with this email address.");
                return;
            }

            generatedCode = GenerateRandomCode();
            userEmail = email;

            MessageBox.Show($"Verification Code:\n\n" +
                          $"{generatedCode}\n\n" +
                          $"Code has been \"sent\" to: {email}",
                          "Verification Code",
                          MessageBoxButtons.OK,
                          MessageBoxIcon.Information);

            string enteredCode = ShowInputDialog("Enter the 6-digit verification code:", "Verify Code");

            if (string.IsNullOrWhiteSpace(enteredCode) || enteredCode.Trim() != generatedCode)
            {
                MessageBox.Show("Invalid verification code. Please try again.");
                return;
            }

            MessageBox.Show("Code verified successfully!");
            ResetPassword resetForm = new ResetPassword();
            resetForm.UserEmail = userEmail;
            this.Hide();
            resetForm.ShowDialog();
            this.Close();
        }

        private void Resend_Click(object sender, EventArgs e)
        {
            string email = txtEmail.Text.Trim();

            if (string.IsNullOrWhiteSpace(email) || email == "Email@gmail.com")
            {
                MessageBox.Show("Please enter your email address first.");
                return;
            }

            if (!EmailExists(email))
            {
                MessageBox.Show("No account found with this email address.");
                return;
            }

            generatedCode = GenerateRandomCode();
            userEmail = email;

            MessageBox.Show($"New Verification Code:\n\n" +
                          $"{generatedCode}\n\n" +
                          $"A new code has been \"sent\" to: {email}",
                          "Code Resent",
                          MessageBoxButtons.OK,
                          MessageBoxIcon.Information);

            string enteredCode = ShowInputDialog("Enter the NEW 6-digit verification code:", "Verify Code");

            if (string.IsNullOrWhiteSpace(enteredCode) || enteredCode.Trim() != generatedCode)
            {
                MessageBox.Show("Invalid verification code. Please try again.");
                return;
            }

            MessageBox.Show("Code verified successfully!");
            ResetPassword resetForm = new ResetPassword();
            resetForm.UserEmail = userEmail;
            this.Hide();
            resetForm.ShowDialog();
            this.Close();
        }

        private string GenerateRandomCode()
        {
            Random rand = new Random();
            return rand.Next(100000, 999999).ToString();
        }

        private bool EmailExists(string email)
        {
            string connString = "Server=.\\SQLEXPRESS; Database=FinalProjectJDRENTALS; Trusted_Connection=True; TrustServerCertificate=True;";
            using (SqlConnection conn = new SqlConnection(connString))
            {
                try
                {
                    conn.Open();
                    string query = "SELECT COUNT(*) FROM Users WHERE TRIM(Email) = @email AND Status = 'Active'";
                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@email", email);
                        int count = (int)cmd.ExecuteScalar();
                        return count > 0;
                    }
                }
                catch
                {
                    return false;
                }
            }
        }

        private string ShowInputDialog(string prompt, string title)
        {
            Form promptForm = new Form()
            {
                Width = 400,
                Height = 180,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                Text = title,
                StartPosition = FormStartPosition.CenterScreen,
                MaximizeBox = false,
                MinimizeBox = false
            };

            Label textLabel = new Label() { Left = 20, Top = 20, Text = prompt, Width = 340 };
            TextBox textBox = new TextBox() { Left = 20, Top = 50, Width = 340 };
            Button confirmation = new Button() { Text = "OK", Left = 260, Top = 90, Width = 100, DialogResult = DialogResult.OK };

            promptForm.Controls.Add(textLabel);
            promptForm.Controls.Add(textBox);
            promptForm.Controls.Add(confirmation);
            promptForm.AcceptButton = confirmation;

            return promptForm.ShowDialog() == DialogResult.OK ? textBox.Text : "";
        }

        private void ForgotPassword_Load(object sender, EventArgs e)
        {
            if (txtEmail != null) txtEmail.Focus();
        }

        private void lblClose_Click(object sender, EventArgs e)
        {
            DialogResult result = MessageBox.Show(
                "Are you sure you want to close this window?",
                "Confirm Close",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result == DialogResult.Yes)
            {
                LogIn loginForm = new LogIn();
                loginForm.Show();
                this.Close();
            }
        }

        private void txtEmail_Enter(object sender, EventArgs e)
        {
            if (txtEmail.Text == "Email@gmail.com")
            {
                txtEmail.Text = "";
            }
        }

        private void txtEmail_Leave(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtEmail.Text))
            {
                txtEmail.Text = "Email@gmail.com";
            }
        }

        private void ForgotPassword_Shown(object sender, EventArgs e)
        {
            panel1.Focus();
        }
    }
}
