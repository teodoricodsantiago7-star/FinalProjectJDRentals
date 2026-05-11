using System;
using System.Data.SqlClient;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace FinalProject
{
    public partial class ResetPassword : Form
    {
        [DllImport("user32.dll")]
        public static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);
        [DllImport("user32.dll")]
        public static extern bool ReleaseCapture();

        private void ResetPassword_MouseDown_1(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                ReleaseCapture();
                SendMessage(Handle, 0xA1, 0x2, 0);
            }
        }
        public string UserEmail { get; set; }

        public ResetPassword()
        {
            InitializeComponent();
        }

        private void btnVerifyPassword_Click(object sender, EventArgs e)
        {
            string newPassword = txtChangePassword.Text.Trim();
            string confirmPassword = txtConfirmPassword.Text.Trim();

            if (string.IsNullOrWhiteSpace(newPassword) || string.IsNullOrWhiteSpace(confirmPassword))
            {
                MessageBox.Show("Please enter and confirm your new password.");
                return;
            }

            if (newPassword != confirmPassword)
            {
                MessageBox.Show("Passwords do not match!");
                return;
            }

            if (newPassword.Length < 8 ||
                !newPassword.Any(char.IsUpper) ||
                !newPassword.Any(char.IsLower) ||
                !newPassword.Any(char.IsDigit))
            {
                MessageBox.Show("Password must be at least 8 characters long\n" +
                              "contain at least one uppercase letter\n" +
                              "contain at least one lowercase letter\n" +
                              "contain at least one number.");
                return;
            }

            if (string.IsNullOrEmpty(UserEmail))
            {
                MessageBox.Show("Error: Email information is missing.");
                return;
            }

            if (UpdatePasswordInDatabase(UserEmail, newPassword))
            {
                MessageBox.Show("Your password has been reset successfully!", "Success");

                LogIn loginForm = new LogIn();
                this.Hide();
                loginForm.Show();
                this.Close();
            }
            else
            {
                MessageBox.Show("Failed to reset password. Please try again.");
            }
        }

        private bool UpdatePasswordInDatabase(string email, string newPassword)
        {
            string connString = "Server=.\\SQLEXPRESS; Database=FinalProjectJDRENTALS; Trusted_Connection=True; TrustServerCertificate=True;";

            using (SqlConnection conn = new SqlConnection(connString))
            {
                try
                {
                    conn.Open();

                    string query = "UPDATE Users SET PasswordHash = @password WHERE TRIM(Email) = @email AND Status = 'Active'";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@password", newPassword);
                        cmd.Parameters.AddWithValue("@email", email);

                        int rowsAffected = cmd.ExecuteNonQuery();
                        return rowsAffected > 0;
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Database Error: " + ex.Message);
                    return false;
                }
            }
        }

        private void ResetPassword_Load(object sender, EventArgs e)
        {
            if (txtChangePassword != null)
                txtChangePassword.Focus();
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

        private void txtChangePassword_Enter(object sender, EventArgs e)
        {
            if (txtChangePassword.Text == "Change Password")
            {
                txtChangePassword.Text = "";
                if (!checkBoxShowPassword.Checked)
                {
                    txtChangePassword.PasswordChar = '*';
                }
            }
        }

        private void txtChangePassword_Leave(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtChangePassword.Text))
            {
                txtChangePassword.Text = "Change Password";
                txtChangePassword.PasswordChar = '\0';
            }
        }

        private void txtConfirmPassword_Enter(object sender, EventArgs e)
        {
            if (txtConfirmPassword.Text == "Confirm Password")
            {
                txtConfirmPassword.Text = "";
                if (!checkBoxShowPassword.Checked)
                {
                    txtConfirmPassword.PasswordChar = '*';
                }
            }
        }

        private void txtConfirmPassword_Leave(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtConfirmPassword.Text))
            {
                txtConfirmPassword.Text = "Confirm Password";
                txtConfirmPassword.PasswordChar = '\0';
            }
        }

        private void checkBoxShowPassword_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBoxShowPassword.Checked)
            {
                txtChangePassword.PasswordChar = '\0';
                txtConfirmPassword.PasswordChar = '\0';
            }
            else
            {
                if (txtChangePassword.Text == "Change Password" || string.IsNullOrWhiteSpace(txtChangePassword.Text))
                {
                    txtChangePassword.PasswordChar = '\0';
                }
                else
                {
                    txtChangePassword.PasswordChar = '*';
                }

                if (txtConfirmPassword.Text == "Confirm Password" || string.IsNullOrWhiteSpace(txtConfirmPassword.Text))
                {
                    txtConfirmPassword.PasswordChar = '\0';
                }
                else
                {
                    txtConfirmPassword.PasswordChar = '*';
                }
            }
        }

    }
}