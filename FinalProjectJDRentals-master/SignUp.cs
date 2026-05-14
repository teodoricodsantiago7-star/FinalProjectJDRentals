using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Text.RegularExpressions;

namespace FinalProject
{
    public partial class SignUp : Form
    {
        public const int WM_NCLBUTTONDOWN = 0xA1;
        public const int HT_CAPTION = 0x2;

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        public static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        public static extern bool ReleaseCapture();
        public SignUp()
        {
            InitializeComponent();
        }

        private void SignUp_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                ReleaseCapture();
                SendMessage(Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0);
            }
        }

        private void btn_Browse_Click(object sender, EventArgs e)
        {
            OpenFileDialog open = new OpenFileDialog();
            open.Filter = "Image Files(*.jpg; *.jpeg; *.gif; *.bmp)|*.jpg; *.jpeg; *.gif; *.bmp";
            if (open.ShowDialog() == DialogResult.OK)
            {
                pictureBoxPhoto.Image = new Bitmap(open.FileName);
                pictureBoxPhoto.Tag = open.FileName;
            }
        }

        private void btnRegister_Click(object sender, EventArgs e)
        {
            string emailPattern = @"^[^@\s]+@[^@\s]+\.[^@\s]+$";
            if (!Regex.IsMatch(txtEmail.Text.Trim(), emailPattern))
            {
                MessageBox.Show("Please enter a valid email address (e.g., name@gmail.com).");
                return;
            }

            if (txtPassword.Text != txtConfirmPassword.Text)
            {
                MessageBox.Show("Passwords do not match!");
                return;
            }
            string pass = txtPassword.Text;
            if (pass.Length < 8 || !pass.Any(char.IsUpper) || !pass.Any(char.IsLower) || !pass.Any(char.IsDigit))
            {
                MessageBox.Show("Password must be " +
                            "\nat least 8 characters long " +
                            "\ncontain at least one uppercase letter " +
                            "\ncontain at least one lowercase letter " +
                            "\ncontain at least one number.");
                return;
            }

            DateTime today = DateTime.Today;
            int age = today.Year - dtpBirthday.Value.Year;
            if (dtpBirthday.Value.Date > today.AddYears(-age)) age--;

            if (age < 15)
            {
                MessageBox.Show("You must be at least 15 years old to register.");
                return;
            }

            string connString = "Server=.\\SQLEXPRESS; Database=FinalProjectJDRENTALS; Trusted_Connection=True; TrustServerCertificate=True;";

            using (SqlConnection conn = new SqlConnection(connString))
            {
                try
                {
                    conn.Open();

                    string checkEmail = "SELECT COUNT(*) FROM Users WHERE Email = @email";
                    SqlCommand checkCmd = new SqlCommand(checkEmail, conn);
                    checkCmd.Parameters.AddWithValue("@email", txtEmail.Text.Trim());
                    int exists = (int)checkCmd.ExecuteScalar();

                    if (exists > 0)
                    {
                        MessageBox.Show("This email is already registered.");
                        return;
                    }

                    string query = @"INSERT INTO Users (Email, PasswordHash, FullName, FirstName, MiddleName, LastName, Gender, Birthday, ImagePath, Role, Status) 
                            VALUES (@email, @pass, @full, @first, @mid, @last, @gender, @birth, @img, 'Staff', 'Active')";

                    SqlCommand cmd = new SqlCommand(query, conn);

                    cmd.Parameters.AddWithValue("@email", txtEmail.Text.Trim());
                    cmd.Parameters.AddWithValue("@pass", txtPassword.Text.Trim());
                    cmd.Parameters.AddWithValue("@full", $"{txtFirstName.Text.Trim()} {txtLastName.Text.Trim()}");
                    cmd.Parameters.AddWithValue("@first", txtFirstName.Text.Trim());
                    cmd.Parameters.AddWithValue("@mid", txtMiddleName.Text.Trim());
                    cmd.Parameters.AddWithValue("@last", txtLastName.Text.Trim());
                    cmd.Parameters.AddWithValue("@gender", rbMale.Checked ? "Male" : rbFemale.Checked ? "Female" : "Other");
                    cmd.Parameters.AddWithValue("@birth", dtpBirthday.Value);
                    cmd.Parameters.AddWithValue("@img", pictureBoxPhoto.Tag?.ToString() ?? "");

                    cmd.ExecuteNonQuery();
                    MessageBox.Show("Account Created Successfully!");

                    LogIn login = new LogIn();
                    login.Show();
                    this.Hide();
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Database Error: " + ex.Message);
                }
            }
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            DialogResult result = MessageBox.Show(
                "All unsaved changes will be lost" +
                "\nAre you sure you want to cancel this registration?",
                "Confirm Cancel",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result == DialogResult.Yes)
            {
                LogIn loginForm = new LogIn();
                loginForm.Show();
                this.Close();
            }
        }

        private void Linklbl_ReturnToLogin_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            DialogResult result = MessageBox.Show(
                "All unsaved changes will be lost" +
                "\nAre you sure you want to return to log-in?",
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

        private void lblClose_Click(object sender, EventArgs e)
        {
            DialogResult result = MessageBox.Show(
            "All unsaved changes will be lost" +
            "\nAre you sure you want to close this window?",
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

        private void SetPlaceholder(object sender, string placeholder)
        {
            TextBox tb = sender as TextBox;
            if (tb == null) return;

            if (tb.Focused)
            {
                if (tb.Text.Trim() == placeholder)
                {
                    tb.Text = "";
                    tb.ForeColor = Color.Black;
                    if (tb.Name.ToLower().Contains("password")) tb.PasswordChar = '*';
                }
            }
            else
            {
                if (string.IsNullOrWhiteSpace(tb.Text))
                {
                    tb.Text = placeholder;
                    tb.ForeColor = Color.DarkGray;
                    if (tb.Name.ToLower().Contains("password")) tb.PasswordChar = '\0';
                }
            }
        }

        private void txtFirstName_Enter(object sender, EventArgs e) => SetPlaceholder(sender, "e.g. Juan");
        private void txtFirstName_Leave(object sender, EventArgs e) => SetPlaceholder(sender, "e.g. Juan");

        private void txtMiddleName_Enter(object sender, EventArgs e) => SetPlaceholder(sender, "e.g. Santos");
        private void txtMiddleName_Leave(object sender, EventArgs e) => SetPlaceholder(sender, "e.g. Santos");

        private void txtLastName_Enter(object sender, EventArgs e) => SetPlaceholder(sender, "e.g. Dela Cruz");
        private void txtLastName_Leave(object sender, EventArgs e) => SetPlaceholder(sender, "e.g. Dela Cruz");

        private void txtEmail_Enter(object sender, EventArgs e) => SetPlaceholder(sender, "e.g. Juan@gmail.com");
        private void txtEmail_Leave(object sender, EventArgs e) => SetPlaceholder(sender, "e.g. Juan@gmail.com");

        private void txtPassword_Enter(object sender, EventArgs e) => SetPlaceholder(sender, "e.g. Abc@1234");
        private void txtPassword_Leave(object sender, EventArgs e) => SetPlaceholder(sender, "e.g. Abc@1234");

        private void txtConfirmPassword_Enter(object sender, EventArgs e) => SetPlaceholder(sender, "e.g. Abc@1234");
        private void txtConfirmPassword_Leave(object sender, EventArgs e) => SetPlaceholder(sender, "e.g. Abc@1234");

        private void SignUp_Shown(object sender, EventArgs e)
        {
            panel2.Focus();
        }

        private void checkBox2_CheckedChanged(object sender, EventArgs e)
        {
            string placeholder = "e.g. Abc@1234";
            if (checkBox2.Checked)
            {
                txtPassword.PasswordChar = '\0';
                txtConfirmPassword.PasswordChar = '\0';
            }
            else
            {
                if (txtPassword.Text.Trim() == placeholder || string.IsNullOrWhiteSpace(txtPassword.Text))
                {
                    txtPassword.PasswordChar = '\0';
                }
                else
                {
                    txtPassword.PasswordChar = '*';
                }
                if (txtConfirmPassword.Text.Trim() == placeholder || string.IsNullOrWhiteSpace(txtConfirmPassword.Text))
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
