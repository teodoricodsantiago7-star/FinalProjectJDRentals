using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace FinalProject
{
    public partial class Customer_Records : Form
    {
        private readonly string connectionString = @"Server=localhost\SQLEXPRESS;Database=FinalProjectJDRENTALS;Trusted_Connection=True;";
        private int currentLoggedInUserId;

        public Customer_Records()
        {
            InitializeComponent();
            this.currentLoggedInUserId = 1;
            ConfigureCustomerGrid();
            RefreshCustomerData();
            LoadUserProfilePicture();
        }

        public Customer_Records(int loggedInUserId)
        {
            InitializeComponent();
            this.currentLoggedInUserId = loggedInUserId > 0 ? loggedInUserId : 1;
            ConfigureCustomerGrid();
            RefreshCustomerData();
            LoadUserProfilePicture();
        }

        private void Customer_Records_Load(object sender, EventArgs e)
        {
            ConfigureCustomerGrid();
            RefreshCustomerData();
            LoadUserProfilePicture();
        }

        private void ConfigureCustomerGrid()
        {
            if (dataGridView1 == null) return;

            dataGridView1.AutoGenerateColumns = false;
            dataGridView1.Columns.Clear();

            dataGridView1.AllowUserToAddRows = false;
            dataGridView1.AllowUserToDeleteRows = false;
            dataGridView1.ReadOnly = false;
            dataGridView1.RowHeadersVisible = false;
            dataGridView1.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dataGridView1.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;

            dataGridView1.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "CustomerID",
                HeaderText = "ID",
                DataPropertyName = "CustomerID",
                ReadOnly = true,
                Visible = false
            });

            dataGridView1.Columns.Add(new DataGridViewTextBoxColumn { Name = "Name", HeaderText = "Name", DataPropertyName = "Name", ReadOnly = true });
            dataGridView1.Columns.Add(new DataGridViewTextBoxColumn { Name = "ContactNo", HeaderText = "Contact Number", DataPropertyName = "ContactNo", ReadOnly = true });
            dataGridView1.Columns.Add(new DataGridViewTextBoxColumn { Name = "Address", HeaderText = "Address", DataPropertyName = "Address", ReadOnly = true });
            dataGridView1.Columns.Add(new DataGridViewTextBoxColumn { Name = "TotalRentals", HeaderText = "Total Rentals", DataPropertyName = "TotalRentals", ReadOnly = true });

            var actionButtonCol = new DataGridViewButtonColumn
            {
                Name = "Action",
                HeaderText = "Action",
                Text = "View History",
                UseColumnTextForButtonValue = true
            };
            dataGridView1.Columns.Add(actionButtonCol);

            dataGridView1.CellContentClick += DataGridView1_CellContentClick;
        }

        private void RefreshCustomerData()
        {
            if (dataGridView1 == null) return;

            string query = "SELECT CustomerID, Name, ContactNo, Address, TotalRentals FROM Customers ORDER BY Name ASC;";
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    try
                    {
                        conn.Open();
                        SqlDataAdapter da = new SqlDataAdapter(cmd);
                        DataTable dt = new DataTable();
                        da.Fill(dt);

                        dataGridView1.DataSource = null;
                        dataGridView1.DataSource = dt;
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Failed to load customer records: " + ex.Message, "Database Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void DataGridView1_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex != dataGridView1.Columns["Action"].Index) return;

            int customerId = Convert.ToInt32(dataGridView1.Rows[e.RowIndex].Cells["CustomerID"].Value);
            string customerName = dataGridView1.Rows[e.RowIndex].Cells["Name"].Value.ToString();

            decimal totalSpent = 0;
            DataTable historyTable = new DataTable();

            string dataQuery = @"
                SELECT (SELECT ISNULL(SUM(AmountPaid), 0) FROM RentalTransactions WHERE CustomerID = @CustomerID AND Status <> 'Cancelled') AS TotalSpent;

                SELECT t.TransactionID AS [Tx ID], 
                       i.ItemName AS [Item], 
                       rd.Quantity AS [Qty], 
                       t.TotalAmount AS [Cost], 
                       t.Status AS [Status], 
                       CONVERT(VARCHAR(10), t.RentalStartDate, 101) AS [Start Date]
                FROM RentalTransactions t
                INNER JOIN RentalDetails rd ON t.TransactionID = rd.TransactionID
                INNER JOIN Items i ON rd.ItemID = i.ItemID
                WHERE t.CustomerID = @CustomerID
                ORDER BY t.TransactionID DESC;";

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                using (SqlCommand cmd = new SqlCommand(dataQuery, conn))
                {
                    cmd.Parameters.AddWithValue("@CustomerID", customerId);
                    try
                    {
                        conn.Open();
                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                totalSpent = Convert.ToDecimal(reader["TotalSpent"]);
                            }

                            if (reader.NextResult())
                            {
                                historyTable.Load(reader);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Error loading customer details: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }
                }
            }

            Form modal = new Form()
            {
                Width = 650,
                Height = 460,
                Text = $"Customer Profile - {customerName}",
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false,
                BackColor = Color.White
            };

            Label lblTotal = new Label()
            {
                Left = 20,
                Top = 15,
                Width = 600,
                Font = new Font("Arial", 11, FontStyle.Bold),
                Text = $"Total Spent: ₱{totalSpent:N2}"
            };

            Label lblNotesHeader = new Label()
            {
                Left = 20,
                Top = 45,
                Width = 600,
                Font = new Font("Arial", 9, FontStyle.Bold),
                Text = "Transaction Notes:"
            };

            TextBox txtNotesBox = new TextBox()
            {
                Left = 20,
                Top = 65,
                Width = 590,
                Height = 60,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                Text = "Please click a row in the rental history below to read its transaction notes.",
                BackColor = Color.WhiteSmoke
            };

            Label lblGridHeader = new Label()
            {
                Left = 20,
                Top = 140,
                Width = 600,
                Font = new Font("Arial", 9, FontStyle.Bold),
                Text = "Rental History:"
            };

            DataGridView dgvHistory = new DataGridView()
            {
                Left = 20,
                Top = 160,
                Width = 590,
                Height = 180,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = true,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                BackgroundColor = Color.WhiteSmoke,
                DataSource = historyTable
            };

            Button btnClose = new Button()
            {
                Text = "Close",
                Left = 510,
                Top = 365,
                Width = 100,
                Height = 30,
                DialogResult = DialogResult.OK
            };

            dgvHistory.CellClick += (s, ev) =>
            {
                if (ev.RowIndex < 0) return;

                object rawTxId = dgvHistory.Rows[ev.RowIndex].Cells["Tx ID"].Value;
                if (rawTxId == null) return;

                int txId = Convert.ToInt32(rawTxId);
                string detailsQuery = "SELECT ISNULL(Notes, '') AS MainNotes FROM RentalTransactions WHERE TransactionID = @TxID;";

                using (SqlConnection conn = new SqlConnection(connectionString))
                using (SqlCommand cmd = new SqlCommand(detailsQuery, conn))
                {
                    cmd.Parameters.AddWithValue("@TxID", txId);
                    try
                    {
                        conn.Open();
                        object result = cmd.ExecuteScalar();
                        string notesText = result != null ? result.ToString() : "";

                        if (string.IsNullOrWhiteSpace(notesText))
                        {
                            txtNotesBox.Text = "No notes recorded for this transaction.";
                        }
                        else
                        {
                            txtNotesBox.Text = notesText;
                        }

                        lblNotesHeader.Text = $"Transaction Notes (ID: {txId}):";
                    }
                    catch (Exception ex)
                    {
                        txtNotesBox.Text = "Could not load transaction notes: " + ex.Message;
                    }
                }
            };

            modal.Controls.Add(lblTotal);
            modal.Controls.Add(lblNotesHeader);
            modal.Controls.Add(txtNotesBox);
            modal.Controls.Add(lblGridHeader);
            modal.Controls.Add(dgvHistory);
            modal.Controls.Add(btnClose);
            modal.AcceptButton = btnClose;

            modal.ShowDialog();
            modal.Dispose();
        }

        private void LoadUserProfilePicture()
        {
            string query = "SELECT ImagePath, FullName FROM Users WHERE UserID = @UserID;";
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@UserID", currentLoggedInUserId);
                    try
                    {
                        conn.Open();
                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                if (UserNameHeader != null)
                                {
                                    UserNameHeader.Text = reader["FullName"] != DBNull.Value
                                        ? reader["FullName"].ToString()
                                        : "Staff Member";
                                }

                                if (pbProfilePic != null)
                                {
                                    if (reader["ImagePath"] != DBNull.Value)
                                    {
                                        string path = reader["ImagePath"].ToString();
                                        if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                                        {
                                            pbProfilePic.Image?.Dispose();
                                            byte[] bytes = File.ReadAllBytes(path);
                                            using (MemoryStream ms = new MemoryStream(bytes))
                                            {
                                                pbProfilePic.Image = Image.FromStream(ms);
                                            }
                                            pbProfilePic.SizeMode = PictureBoxSizeMode.Zoom;
                                        }
                                        else
                                        {
                                            pbProfilePic.Image = null;
                                        }
                                    }
                                    else
                                    {
                                        pbProfilePic.Image = null;
                                    }
                                }
                            }
                        }
                    }
                    catch
                    {
                        if (pbProfilePic != null) pbProfilePic.Image = null;
                    }
                }
            }
        }

        private void btnHome_Click(object sender, EventArgs e)
        {
            SafelyNavigateToForm(new DashBoard1(this.currentLoggedInUserId));
        }

        private void btnNewRentalTransaction_Click(object sender, EventArgs e)
        {
            SafelyNavigateToForm(new NewRentalTransaction(this.currentLoggedInUserId));
        }

        private void btnCalendar_Click(object sender, EventArgs e)
        {
            SafelyNavigateToForm(new Calendar(this.currentLoggedInUserId));
        }

        private void btnInventoryManagement_Click(object sender, EventArgs e)
        {
            SafelyNavigateToForm(new Inventory_Management(this.currentLoggedInUserId));
        }

        private void btnBookingManagement_Click(object sender, EventArgs e)
        {
            SafelyNavigateToForm(new Booking_Management(this.currentLoggedInUserId));
        }

        private void SafelyNavigateToForm(Form targetForm)
        {
            if (pbProfilePic != null && pbProfilePic.Image != null)
            {
                pbProfilePic.Image.Dispose();
                pbProfilePic.Image = null;
            }
            this.FormClosed -= (s, a) => Application.Exit();
            targetForm.FormClosed += (s, a) => Application.Exit();
            this.Hide();
            targetForm.Show();
            this.Dispose();
        }
    }
}
