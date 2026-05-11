using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Data.SqlClient;
using System.Text.RegularExpressions;

namespace FinalProject
{
    public partial class LogIn : Form
    {
        public bool AttemptLogin(string emailInput, string passwordInput)
        {
            string connectionString = "Server=.\\SQLEXPRESS; Database=FinalProjectJDRENTALS; Trusted_Connection=True; TrustServerCertificate=True;";

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                string query = "SELECT UserID, FullName, Role FROM Users WHERE TRIM(Email) = @email AND TRIM(PasswordHash) = @password AND Status = 'Active'";

                SqlCommand cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@email", emailInput.Trim());
                cmd.Parameters.AddWithValue("@password", passwordInput.Trim());

                try
                {
                    conn.Open();
                    SqlDataReader reader = cmd.ExecuteReader();

                    if (reader.HasRows)
                    {
                        reader.Read();
                        string fullName = reader["FullName"].ToString();
                        string role = reader["Role"].ToString();

                        MessageBox.Show($"Welcome back, {fullName}! (Role: {role})");
                        return true;
                    }
                    else
                    {
                        MessageBox.Show("Invalid email or password.");
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Connection Error: " + ex.Message);
                    return false;
                }
            }
        }

        [DllImport("Gdi32.dll", EntryPoint = "CreateRoundRectRgn")]
        private static extern IntPtr CreateRoundRectRgn
        (
            int nLeftRect,
            int nTopRect,
            int nRightRect,
            int nBottomRect,
            int nWidthEllipse,
            int nHeightEllipse
        );


        [DllImport("user32.dll")]
        public static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        public static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);

        public LogIn()
        {
            InitializeComponent();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left)
            {
                ReleaseCapture();
                SendMessage(Handle, 0xA1, 0x2, 0);
            }
        }

        private void txtB_Email_Enter(object sender, EventArgs e)
        {
            if (txtB_Email.Text == "Email")
            {
                txtB_Email.Text = "";
            }
        }

        private void txtB_Email_Leave(object sender, EventArgs e)
        {
            if (txtB_Email.Text == "")
            {
                txtB_Email.Text = "Email";
            }
        }

        private void txtB_Password_Enter(object sender, EventArgs e)
        {
            if (txtB_Password.Text == "Password")
            {
                txtB_Password.Text = "";

                if (Chbox1.Checked == false)
                {
                    txtB_Password.PasswordChar = '*';
                }
            }
        }

        private void txtB_Password_Leave(object sender, EventArgs e)
        {
            if (txtB_Password.Text == "")
            {
                txtB_Password.PasswordChar = '\0';
                txtB_Password.Text = "Password";
            }
        }

        private void checkBox2_CheckedChanged(object sender, EventArgs e)
        {
            if (Chbox1.Checked == true)
            {
                txtB_Password.PasswordChar = '\0';
            }
            else
            {
                if (txtB_Password.Text != "Password")
                {
                    txtB_Password.PasswordChar = '*';
                }
            }
        }

        private void lbl_Exit_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        private void LogIn_Shown(object sender, EventArgs e)
        {
            MainPanel1.Focus();
        }

        private void Linklbl_ForgetPassword_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            this.Hide();
            ForgotPassword fp = new ForgotPassword();
            fp.Show();
        }

        private void btn_Login_Click(object sender, EventArgs e)
        {
            string email = txtB_Email.Text;
            string pass = txtB_Password.Text;

            if (email == "Email" || pass == "Password")
            {
                MessageBox.Show("Please enter your credentials.");
                return;
            }

            if (AttemptLogin(email, pass))
            {
                DashBoard1 main = new DashBoard1();
                main.Show();
                this.Hide();
            }
        }

        private void Linklbl_SignUpNow_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            SignUp signupForm = new SignUp();
            signupForm.Show();
            this.Hide();
        }
    }
}
//Yoo
