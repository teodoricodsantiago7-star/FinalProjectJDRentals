using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace FinalProject
{
    public partial class Booking_Management : Form
    {
        private readonly string connectionString = @"Server=localhost\SQLEXPRESS;Database=FinalProjectJDRENTALS;Trusted_Connection=True;";
        private int currentLoggedInUserId;

        public Booking_Management()
        {
            InitializeComponent();
            this.currentLoggedInUserId = 1;
            SetupFormDefaults();
        }

        public Booking_Management(int loggedInUserId)
        {
            InitializeComponent();
            this.currentLoggedInUserId = loggedInUserId > 0 ? loggedInUserId : 1;
            SetupFormDefaults();
        }

        private void Booking_Management_Load(object sender, EventArgs e)
        {
            SetupFormDefaults();
        }

        private void SetupFormDefaults()
        {
            ConfigureBookingGrid();
            LoadFilterDropdown();
            RefreshBookingData();
            LoadUserProfilePicture();
        }

        private void LoadFilterDropdown()
        {
            if (cmbFilters == null) return;
            cmbFilters.SelectedIndexChanged -= cmbFilters_SelectedIndexChanged;
            cmbFilters.Items.Clear();
            cmbFilters.Items.AddRange(new string[] { "All", "Pending", "Confirmed", "Overdue", "Cancelled" });
            cmbFilters.SelectedIndex = 0;
            cmbFilters.SelectedIndexChanged += cmbFilters_SelectedIndexChanged;
        }

        private void ConfigureBookingGrid()
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
                Name = "TransactionID",
                HeaderText = "ID",
                DataPropertyName = "TransactionID",
                ReadOnly = true,
                Visible = false
            });

            dataGridView1.Columns.Add(new DataGridViewTextBoxColumn { Name = "CustomerName", HeaderText = "Customer Name", DataPropertyName = "CustomerName", ReadOnly = true });
            dataGridView1.Columns.Add(new DataGridViewTextBoxColumn { Name = "ItemName", HeaderText = "Rented Items", DataPropertyName = "ItemName", ReadOnly = true });

            var startCol = new DataGridViewTextBoxColumn { Name = "RentalStartDate", HeaderText = "Start Date", DataPropertyName = "RentalStartDate", ReadOnly = true };
            startCol.DefaultCellStyle.Format = "MM/dd/yyyy hh:mm tt";
            dataGridView1.Columns.Add(startCol);

            var returnCol = new DataGridViewTextBoxColumn { Name = "ExpectedReturnDate", HeaderText = "Return Date", DataPropertyName = "ExpectedReturnDate", ReadOnly = true };
            returnCol.DefaultCellStyle.Format = "MM/dd/yyyy hh:mm tt";
            dataGridView1.Columns.Add(returnCol);

            dataGridView1.Columns.Add(new DataGridViewTextBoxColumn { Name = "Status", HeaderText = "Status", DataPropertyName = "Status", ReadOnly = true });

            var actionButtonCol = new DataGridViewButtonColumn
            {
                Name = "Action",
                HeaderText = "Action",
                Text = "Edit Details",
                UseColumnTextForButtonValue = true
            };
            dataGridView1.Columns.Add(actionButtonCol);

            dataGridView1.CellContentClick += DataGridView1_CellContentClick;
        }

        private void RefreshBookingData()
        {
            if (dataGridView1 == null) return;

            string filterStatus = cmbFilters != null ? cmbFilters.SelectedItem?.ToString() : "All";
            string searchKeyword = txtSearch != null ? txtSearch.Text.Trim() : "";

            string query = @"
                SELECT t.TransactionID, c.Name AS CustomerName, i.ItemName, t.RentalStartDate, t.ExpectedReturnDate, t.Status
                FROM RentalTransactions t
                INNER JOIN Customers c ON t.CustomerID = c.CustomerID
                INNER JOIN RentalDetails rd ON t.TransactionID = rd.TransactionID
                INNER JOIN Items i ON rd.ItemID = i.ItemID
                WHERE 1=1";

            if (filterStatus != "All" && !string.IsNullOrEmpty(filterStatus))
            {
                query += " AND t.Status = @Status";
            }
            if (!string.IsNullOrEmpty(searchKeyword))
            {
                query += " AND (c.Name LIKE '%' + @Search + '%' OR i.ItemName LIKE '%' + @Search + '%')";
            }
            query += " ORDER BY t.TransactionID DESC;";

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    if (filterStatus != "All" && !string.IsNullOrEmpty(filterStatus))
                    {
                        cmd.Parameters.AddWithValue("@Status", filterStatus);
                    }
                    if (!string.IsNullOrEmpty(searchKeyword))
                    {
                        cmd.Parameters.AddWithValue("@Search", searchKeyword);
                    }

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
                        MessageBox.Show("Could not load bookings: " + ex.Message, "Database Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void DataGridView1_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex != dataGridView1.Columns["Action"].Index) return;

            int transactionId = Convert.ToInt32(dataGridView1.Rows[e.RowIndex].Cells["TransactionID"].Value);
            ShowBookingEditModal(transactionId);
        }

        private void ShowBookingEditModal(int txId)
        {
            string fetchQuery = @"
                SELECT t.TransactionID, c.Name AS CustomerName, i.ItemName, rd.Quantity, t.RentalStartDate, t.ExpectedReturnDate,
                       t.TotalAmount, t.DepositAmount, t.AmountPaid, t.PaymentMethod, t.Status, ISNULL(t.Notes, '') AS Notes
                FROM RentalTransactions t
                INNER JOIN Customers c ON t.CustomerID = c.CustomerID
                INNER JOIN RentalDetails rd ON t.TransactionID = rd.TransactionID
                INNER JOIN Items i ON rd.ItemID = i.ItemID
                WHERE t.TransactionID = @TxID;";

            string customerName = "";
            string itemName = "";
            int quantity = 0;
            DateTime start = DateTime.Now;
            DateTime returnDate = DateTime.Now;
            decimal total = 0;
            decimal deposit = 0;
            decimal paid = 0;
            string method = "Cash";
            string currentStatus = "Pending";
            string notes = "";

            using (SqlConnection conn = new SqlConnection(connectionString))
            using (SqlCommand cmd = new SqlCommand(fetchQuery, conn))
            {
                cmd.Parameters.AddWithValue("@TxID", txId);
                try
                {
                    conn.Open();
                    using (SqlDataReader r = cmd.ExecuteReader())
                    {
                        if (r.Read())
                        {
                            customerName = r["CustomerName"].ToString();
                            itemName = r["ItemName"].ToString();
                            quantity = Convert.ToInt32(r["Quantity"]);
                            start = Convert.ToDateTime(r["RentalStartDate"]);
                            returnDate = Convert.ToDateTime(r["ExpectedReturnDate"]);
                            total = Convert.ToDecimal(r["TotalAmount"]);
                            deposit = Convert.ToDecimal(r["DepositAmount"]);
                            paid = Convert.ToDecimal(r["AmountPaid"]);
                            method = r["PaymentMethod"].ToString();
                            currentStatus = r["Status"].ToString();
                            notes = r["Notes"].ToString();
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Could not open booking details: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
            }

            Form modal = new Form()
            {
                Width = 520,
                Height = 480,
                Text = $"Edit Booking - ID {txId}",
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false,
                BackColor = Color.White
            };

            Label lblInfo = new Label() { Left = 20, Top = 15, Width = 460, Font = new Font("Arial", 10, FontStyle.Bold), Text = $"Customer: {customerName}\nItem: {itemName} (Qty: {quantity})\nDates: {start:MM/dd/yyyy} to {returnDate:MM/dd/yyyy}" };
            lblInfo.Height = 55;

            Label lblCosts = new Label() { Left = 20, Top = 80, Width = 460, Font = new Font("Arial", 9, FontStyle.Regular), Text = $"Total Price: ₱{total:N2}   |   Deposit Left: ₱{deposit:N2}" };

            Label lblPaid = new Label() { Left = 20, Top = 120, Width = 150, Text = "Amount Paid (₱):" };
            TextBox txtPaid = new TextBox() { Left = 180, Top = 117, Width = 150, Text = paid.ToString("F2") };

            Label lblMethod = new Label() { Left = 20, Top = 160, Width = 150, Text = "Payment Method:" };
            ComboBox cmbMethod = new ComboBox() { Left = 180, Top = 157, Width = 150, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbMethod.Items.AddRange(new string[] { "Cash", "GCash", "Partial" });
            cmbMethod.SelectedItem = method;

            Label lblStatus = new Label() { Left = 20, Top = 200, Width = 150, Text = "Booking Status:" };
            ComboBox cmbStatus = new ComboBox() { Left = 180, Top = 197, Width = 150, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbStatus.Items.AddRange(new string[] { "Pending", "Confirmed", "Overdue", "Cancelled" });
            cmbStatus.SelectedItem = cmbStatus.Items.Contains(currentStatus) ? currentStatus : "Pending";

            Label lblNotes = new Label() { Left = 20, Top = 240, Width = 150, Text = "Booking Notes:" };
            TextBox txtNotesBox = new TextBox() { Left = 20, Top = 265, Width = 460, Height = 80, Multiline = true, ScrollBars = ScrollBars.Vertical, Text = notes };

            Button btnSave = new Button() { Text = "Save Changes", Left = 260, Top = 370, Width = 100, Height = 30, DialogResult = DialogResult.OK };
            Button btnCancel = new Button() { Text = "Cancel", Left = 380, Top = 370, Width = 100, Height = 30, DialogResult = DialogResult.Cancel };

            modal.Controls.AddRange(new Control[] { lblInfo, lblCosts, lblPaid, txtPaid, lblMethod, cmbMethod, lblStatus, cmbStatus, lblNotes, txtNotesBox, btnSave, btnCancel });
            modal.AcceptButton = btnSave;

            if (modal.ShowDialog() == DialogResult.OK)
            {
                if (!decimal.TryParse(txtPaid.Text.Trim(), out decimal newPaid) || newPaid < 0)
                {
                    MessageBox.Show("Please enter a valid amount paid.", "Input Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                string newStatus = cmbStatus.SelectedItem.ToString();
                string newMethod = cmbMethod.SelectedItem.ToString();
                string newNotes = txtNotesBox.Text.Trim();

                string updateQuery = @"
                    UPDATE RentalTransactions 
                    SET AmountPaid = @Paid, PaymentMethod = @Method, Status = @Status, Notes = @Notes
                    WHERE TransactionID = @TxID;

                    INSERT INTO AuditLog (UserID, ActionType, TableName, RecordID, Description, ActionTime)
                    VALUES (@UserID, 'UPDATE', 'RentalTransactions', @TxID, @Desc, GETDATE());";

                using (SqlConnection conn = new SqlConnection(connectionString))
                using (SqlCommand cmd = new SqlCommand(updateQuery, conn))
                {
                    cmd.Parameters.AddWithValue("@Paid", newPaid);
                    cmd.Parameters.AddWithValue("@Method", newMethod);
                    cmd.Parameters.AddWithValue("@Status", newStatus);
                    cmd.Parameters.AddWithValue("@Notes", newNotes);
                    cmd.Parameters.AddWithValue("@TxID", txId);
                    cmd.Parameters.AddWithValue("@UserID", currentLoggedInUserId);
                    cmd.Parameters.AddWithValue("@Desc", $"Updated booking {txId}. Status: {newStatus}. Payment: {newMethod}.");

                    try
                    {
                        conn.Open();
                        cmd.ExecuteNonQuery();
                        MessageBox.Show("Changes saved successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        RefreshBookingData();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Could not save changes: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
            modal.Dispose();
        }

        private void btnSearch_Click(object sender, EventArgs e)
        {
            RefreshBookingData();
        }

        private void btnRefresh_Click(object sender, EventArgs e)
        {
            if (txtSearch != null) txtSearch.Text = string.Empty;
            if (cmbFilters != null) cmbFilters.SelectedIndex = 0;
            RefreshBookingData();
        }

        private void cmbFilters_SelectedIndexChanged(object sender, EventArgs e)
        {
            RefreshBookingData();
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
                                    UserNameHeader.Text = reader["FullName"] != DBNull.Value ? reader["FullName"].ToString() : "Staff Member";
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
                                        else pbProfilePic.Image = null;
                                    }
                                    else pbProfilePic.Image = null;
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

        private void btnRecords_Click(object sender, EventArgs e)
        {
            SafelyNavigateToForm(new Customer_Records(this.currentLoggedInUserId));
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
