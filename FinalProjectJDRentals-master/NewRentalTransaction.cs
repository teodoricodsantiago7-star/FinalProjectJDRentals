using System;
using System.IO;
using System.Drawing;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Windows.Forms;

namespace FinalProject
{
    public partial class NewRentalTransaction : Form
    {
        private readonly string connectionString = @"Server=localhost\SQLEXPRESS;Database=FinalProjectJDRENTALS;Trusted_Connection=True;";
        private Dictionary<int, decimal> itemRates = new Dictionary<int, decimal>();
        private int currentCustomerId = -1;
        private int currentLoggedInUserId = 1;

        public NewRentalTransaction(int loggedInUserId)
        {
            InitializeComponent();
            this.currentLoggedInUserId = loggedInUserId > 0 ? loggedInUserId : 1;
            SetupFormDefaults();
            LoadUserProfilePicture();
            RefreshSummaryPanels();
            LoadDataGrids();
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

                                if (pbUserImage != null && reader["ImagePath"] != DBNull.Value)
                                {
                                    string imagePath = reader["ImagePath"].ToString();
                                    if (!string.IsNullOrWhiteSpace(imagePath) && File.Exists(imagePath))
                                    {
                                        pbUserImage.Image?.Dispose();
                                        byte[] bytes = File.ReadAllBytes(imagePath);
                                        using (MemoryStream ms = new MemoryStream(bytes))
                                        {
                                            pbUserImage.Image = Image.FromStream(ms);
                                        }
                                        pbUserImage.SizeMode = PictureBoxSizeMode.Zoom;
                                    }
                                }
                            }
                        }
                    }
                    catch
                    {
                        if (pbUserImage != null) pbUserImage.Image = null;
                    }
                }
            }
        }

        private void SetupFormDefaults()
        {
            LoadCustomerDropdown();
            LoadInventoryItems();
            ResetInputFields();

            dtpStartDate.Format = DateTimePickerFormat.Short;
            dtpStartDate.ShowUpDown = false;

            dtpExpectedReturnDate.Format = DateTimePickerFormat.Short;
            dtpExpectedReturnDate.ShowUpDown = false;

            dtpStartTime.Format = DateTimePickerFormat.Time;
            dtpStartTime.ShowUpDown = true;

            dtpExpectedReturnTime.Format = DateTimePickerFormat.Time;
            dtpExpectedReturnTime.ShowUpDown = true;

            txtDailyRate.ReadOnly = true;
            txtSubTotal.ReadOnly = true;
            txtBalanceDue.ReadOnly = true;

            WireUpCalculationEvents();
        }

        private void WireUpCalculationEvents()
        {
            if (cmbItem != null)
            {
                cmbItem.SelectedIndexChanged -= cmbItem_SelectedIndexChanged;
                cmbItem.SelectedIndexChanged += cmbItem_SelectedIndexChanged;
                cmbItem.SelectedValueChanged -= cmbItem_SelectedIndexChanged;
                cmbItem.SelectedValueChanged += cmbItem_SelectedIndexChanged;
            }

            if (numQuantity != null)
            {
                numQuantity.ValueChanged -= numQuantity_ValueChanged;
                numQuantity.ValueChanged += numQuantity_ValueChanged;
            }

            if (numDeposit != null)
            {
                numDeposit.ValueChanged -= (s, e) => CalculateBalance();
                numDeposit.ValueChanged += (s, e) => CalculateBalance();
            }

            if (txtAmountPaid != null)
            {
                txtAmountPaid.TextChanged -= txtAmountPaid_TextChanged;
                txtAmountPaid.TextChanged += txtAmountPaid_TextChanged;
            }

            if (dtpStartDate != null) dtpStartDate.ValueChanged += (s, e) => CalculateTotals();
            if (dtpStartTime != null) dtpStartTime.ValueChanged += (s, e) => CalculateTotals();
            if (dtpExpectedReturnDate != null) dtpExpectedReturnDate.ValueChanged += (s, e) => CalculateTotals();
            if (dtpExpectedReturnTime != null) dtpExpectedReturnTime.ValueChanged += (s, e) => CalculateTotals();
        }

        private void RefreshSummaryPanels()
        {
            string summaryQuery = @"
                SELECT 
                    (SELECT COUNT(*) FROM RentalTransactions WHERE Status = 'Ongoing') AS ActiveRents,
                    (SELECT COUNT(*) FROM RentalTransactions WHERE Status = 'Overdue' OR (ExpectedReturnDate < GETDATE() AND ActualReturnDate IS NULL AND Status <> 'Cancelled')) AS PendingReturns,
                    (SELECT TOP 1 CONCAT('ID: ', TransactionID, ' - ₱', TotalAmount) FROM RentalTransactions ORDER BY TransactionID DESC) AS RecentTx;";

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                using (SqlCommand cmd = new SqlCommand(summaryQuery, conn))
                {
                    try
                    {
                        conn.Open();
                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                if (lblTotalActiveRents != null) lblTotalActiveRents.Text = reader["ActiveRents"].ToString();
                                if (lblPendingReturns != null) lblPendingReturns.Text = reader["PendingReturns"].ToString();
                                if (lblRecentTransaction != null) lblRecentTransaction.Text = reader["RecentTx"] != DBNull.Value ? reader["RecentTx"].ToString() : "None";
                            }
                        }
                    }
                    catch
                    {
                    }
                }
            }
        }

        private void LoadDataGrids()
        {
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                try
                {
                    conn.Open();

                    string logQuery = "SELECT CAST(ActionTime AS DATE) AS [Date], CONVERT(VARCHAR(8), ActionTime, 108) AS [Time], Description FROM AuditLog ORDER BY LogID DESC;";
                    SqlDataAdapter logAdapter = new SqlDataAdapter(logQuery, conn);
                    DataTable logTable = new DataTable();
                    logAdapter.Fill(logTable);

                    if (dgvSystemLogs != null)
                    {
                        dgvSystemLogs.AutoGenerateColumns = true;
                        dgvSystemLogs.DataSource = logTable;
                    }

                    string userQuery = "SELECT FullName AS [Account Name], Role AS [Account Role] FROM Users WHERE Status = 'Active';";
                    SqlDataAdapter userAdapter = new SqlDataAdapter(userQuery, conn);
                    DataTable userTable = new DataTable();
                    userAdapter.Fill(userTable);

                    if (dgvAccountRoles != null)
                    {
                        dgvAccountRoles.AutoGenerateColumns = true;
                        dgvAccountRoles.DataSource = userTable;
                    }
                }
                catch
                {
                }
            }
        }

        private void LoadCustomerDropdown()
        {
            string query = "SELECT CustomerID, Name FROM Customers ORDER BY Name ASC;";
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

                        cmbCustomerName.DataSource = dt;
                        cmbCustomerName.DisplayMember = "Name";
                        cmbCustomerName.ValueMember = "CustomerID";
                        cmbCustomerName.SelectedIndex = -1;
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Failed to load customers: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void LoadInventoryItems()
        {
            string query = "SELECT ItemID, ItemName, DailyRate FROM Items WHERE Status = 'Available' AND AvailableQuantity > 0;";
            itemRates.Clear();

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    try
                    {
                        conn.Open();
                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            DataTable dt = new DataTable();
                            dt.Columns.Add("ItemID", typeof(int));
                            dt.Columns.Add("ItemName", typeof(string));

                            while (reader.Read())
                            {
                                int id = Convert.ToInt32(reader["ItemID"]);
                                decimal rate = Convert.ToDecimal(reader["DailyRate"]);
                                string name = reader["ItemName"].ToString();

                                itemRates.Add(id, rate);
                                dt.Rows.Add(id, name);
                            }

                            cmbItem.DataSource = dt;
                            cmbItem.DisplayMember = "ItemName";
                            cmbItem.ValueMember = "ItemID";
                            cmbItem.SelectedIndex = -1;
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Failed to load inventory items: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void cmbItem_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (cmbItem == null || cmbItem.SelectedIndex == -1 || cmbItem.SelectedValue == null)
            {
                txtDailyRate.Text = "0.00";
                txtSubTotal.Text = "0.00";
                CalculateBalance();
                return;
            }

            CalculateTotals();
        }

        private void numQuantity_ValueChanged(object sender, EventArgs e)
        {
            CalculateTotals();
        }

        private void dtpStartDate_ValueChanged(object sender, EventArgs e)
        {
            CalculateTotals();
        }

        private void dtpStartTime_ValueChanged(object sender, EventArgs e)
        {
            CalculateTotals();
        }

        private void dtpExpectedReturnDate_ValueChanged(object sender, EventArgs e)
        {
            CalculateTotals();
        }

        private void dtpExpectedReturnTime_ValueChanged(object sender, EventArgs e)
        {
            CalculateTotals();
        }

        private void txtAmountPaid_TextChanged(object sender, EventArgs e)
        {
            CalculateBalance();
        }

        private DateTime GetCombinedStartDateTime()
        {
            if (dtpStartDate == null || dtpStartTime == null) return DateTime.Now;

            DateTime datePart = dtpStartDate.Value.Date;
            TimeSpan timePart = dtpStartTime.Value.TimeOfDay;

            return datePart.Add(timePart);
        }

        private DateTime GetCombinedEndDateTime()
        {
            if (dtpExpectedReturnDate == null || dtpExpectedReturnTime == null) return DateTime.Now.AddDays(1);

            DateTime datePart = dtpExpectedReturnDate.Value.Date;
            TimeSpan timePart = dtpExpectedReturnTime.Value.TimeOfDay;

            return datePart.Add(timePart);
        }

        private void CalculateTotals()
        {
            if (cmbItem == null || cmbItem.SelectedIndex == -1 || cmbItem.SelectedValue == null)
            {
                if (txtDailyRate != null) txtDailyRate.Text = "0.00";
                if (txtSubTotal != null) txtSubTotal.Text = "0.00";
                CalculateBalance();
                return;
            }

            if (!int.TryParse(cmbItem.SelectedValue.ToString(), out int itemId)) return;

            if (itemRates != null && itemRates.TryGetValue(itemId, out decimal dailyRate))
            {
                txtDailyRate.Text = dailyRate.ToString("F2");

                int qty = numQuantity != null ? (int)numQuantity.Value : 1;
                DateTime start = GetCombinedStartDateTime();
                DateTime end = GetCombinedEndDateTime();

                double totalHours = (end - start).TotalHours;
                int days = (int)Math.Ceiling(totalHours / 24.0);
                if (days <= 0) days = 1;

                decimal subTotal = dailyRate * qty * days;

                if (txtSubTotal != null)
                {
                    txtSubTotal.Text = subTotal.ToString("F2");
                }

                CalculateBalance();
            }
        }

        private void CalculateBalance()
        {
            if (txtSubTotal == null || txtBalanceDue == null) return;

            if (!decimal.TryParse(txtSubTotal.Text, out decimal subTotal)) subTotal = 0;

            decimal deposit = 0;
            if (numDeposit != null)
            {
                deposit = numDeposit.Value;
            }

            string rawPaidText = txtAmountPaid != null ? txtAmountPaid.Text.Trim() : "0.00";
            decimal amountPaid = 0;

            if (!string.IsNullOrWhiteSpace(rawPaidText) && !decimal.TryParse(rawPaidText, out amountPaid))
            {
                txtBalanceDue.Text = "Invalid Amount";
                return;
            }

            decimal balanceDue = (subTotal + deposit) - amountPaid;
            if (balanceDue < 0) balanceDue = 0;

            txtBalanceDue.Text = balanceDue.ToString("F2");
        }

        private void btnSearch_Click(object sender, EventArgs e)
        {
            string searchName = cmbCustomerName.Text.Trim();
            if (string.IsNullOrWhiteSpace(searchName) || searchName == "Name")
            {
                MessageBox.Show("Please enter a customer name to search.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string query = "SELECT CustomerID, Name FROM Customers WHERE Name LIKE '%' + @SearchName + '%' ORDER BY Name ASC;";

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@SearchName", searchName);
                    try
                    {
                        conn.Open();
                        SqlDataAdapter da = new SqlDataAdapter(cmd);
                        DataTable dt = new DataTable();
                        da.Fill(dt);

                        if (dt.Rows.Count > 0)
                        {
                            cmbCustomerName.DataSource = dt;
                            cmbCustomerName.DisplayMember = "Name";
                            cmbCustomerName.ValueMember = "CustomerID";
                            cmbCustomerName.DroppedDown = true;
                        }
                        else
                        {
                            MessageBox.Show("No matching customers found.", "No Results", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Search failed: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void btnSave_Click(object sender, EventArgs e)
        {
            if (cmbCustomerName.SelectedIndex == -1 || cmbCustomerName.SelectedValue == null)
            {
                MessageBox.Show("Please select a customer from the list.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (cmbItem.SelectedIndex == -1 || cmbItem.SelectedValue == null)
            {
                MessageBox.Show("Please select an item from the list.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            int quantity = (int)numQuantity.Value;
            if (quantity <= 0)
            {
                MessageBox.Show("Quantity must be 1 or more.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            DateTime finalStartDateTime = GetCombinedStartDateTime();
            DateTime finalEndDateTime = GetCombinedEndDateTime();
            if (finalEndDateTime < finalStartDateTime)
            {
                MessageBox.Show("Return date cannot be earlier than the start date.", "Date Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (!decimal.TryParse(txtSubTotal.Text, out decimal totalAmount)) totalAmount = 0;
            decimal amountPaid = 0;
            if (!string.IsNullOrWhiteSpace(txtAmountPaid.Text) && !decimal.TryParse(txtAmountPaid.Text.Trim(), out amountPaid))
            {
                MessageBox.Show("Please enter a valid number for amount paid.", "Format Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            int customerId = Convert.ToInt32(cmbCustomerName.SelectedValue);
            int itemId = Convert.ToInt32(cmbItem.SelectedValue);
            string paymentMethod = rbCash.Checked ? "Cash" : rbGCash.Checked ? "GCash" : "Partial";

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();
                SqlTransaction transaction = conn.BeginTransaction();

                try
                {
                    string checkStockQuery = "SELECT AvailableQuantity FROM Items WHERE ItemID = @ItemID;";
                    using (SqlCommand cmd = new SqlCommand(checkStockQuery, conn, transaction))
                    {
                        cmd.Parameters.AddWithValue("@ItemID", itemId);
                        int available = Convert.ToInt32(cmd.ExecuteScalar());

                        if (available < quantity)
                        {
                            MessageBox.Show($"Not enough stock. Only {available} remaining.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            transaction.Rollback();
                            return;
                        }
                    }

                    string insertTxQuery = @"
                        INSERT INTO RentalTransactions (CustomerID, UserID, RentalStartDate, ExpectedReturnDate, TotalAmount, DepositAmount, AmountPaid, PaymentMethod, Status, Notes, CreatedAt)
                        OUTPUT INSERTED.TransactionID
                        VALUES (@CustomerID, @UserID, @StartDate, @ExpectedReturnDate, @TotalAmount, @DepositAmount, @AmountPaid, @PaymentMethod, 'Ongoing', @Notes, GETDATE());";

                    int newTransactionId;
                    using (SqlCommand cmd = new SqlCommand(insertTxQuery, conn, transaction))
                    {
                        cmd.Parameters.AddWithValue("@CustomerID", customerId);
                        cmd.Parameters.AddWithValue("@UserID", currentLoggedInUserId);
                        cmd.Parameters.AddWithValue("@StartDate", finalStartDateTime);
                        cmd.Parameters.AddWithValue("@ExpectedReturnDate", finalEndDateTime);
                        cmd.Parameters.AddWithValue("@TotalAmount", totalAmount);
                        cmd.Parameters.AddWithValue("@DepositAmount", numDeposit != null ? numDeposit.Value : 0);
                        cmd.Parameters.AddWithValue("@AmountPaid", amountPaid);
                        cmd.Parameters.AddWithValue("@PaymentMethod", paymentMethod);
                        cmd.Parameters.AddWithValue("@Notes", txtNotes.Text.Trim());

                        newTransactionId = Convert.ToInt32(cmd.ExecuteScalar());
                    }

                    string insertDetailQuery = @"
                        INSERT INTO RentalDetails (TransactionID, ItemID, Quantity, Subtotal, ConditionBefore)
                        VALUES (@TransactionID, @ItemID, @Quantity, @Subtotal, 'Good');";

                    using (SqlCommand cmd = new SqlCommand(insertDetailQuery, conn, transaction))
                    {
                        cmd.Parameters.AddWithValue("@TransactionID", newTransactionId);
                        cmd.Parameters.AddWithValue("@ItemID", itemId);
                        cmd.Parameters.AddWithValue("@Quantity", quantity);
                        cmd.Parameters.AddWithValue("@Subtotal", totalAmount);

                        cmd.ExecuteNonQuery();
                    }

                    string updateStockQuery = @"
                        UPDATE Items 
                        SET AvailableQuantity = AvailableQuantity - @Quantity,
                            Status = CASE WHEN (AvailableQuantity - @Quantity) <= 0 THEN 'Fully Booked' ELSE Status END
                        WHERE ItemID = @ItemID;";

                    using (SqlCommand cmd = new SqlCommand(updateStockQuery, conn, transaction))
                    {
                        cmd.Parameters.AddWithValue("@Quantity", quantity);
                        cmd.Parameters.AddWithValue("@ItemID", itemId);

                        cmd.ExecuteNonQuery();
                    }

                    string logActionQuery = @"
                        INSERT INTO AuditLog (UserID, ActionType, TableName, RecordID, Description, ActionTime)
                        VALUES (@UserID, 'INSERT', 'RentalTransactions', @RecordID, @Description, GETDATE());";

                    using (SqlCommand cmd = new SqlCommand(logActionQuery, conn, transaction))
                    {
                        cmd.Parameters.AddWithValue("@UserID", currentLoggedInUserId);
                        cmd.Parameters.AddWithValue("@RecordID", newTransactionId);
                        cmd.Parameters.AddWithValue("@Description", $"Created rental transaction ID {newTransactionId} for Customer ID {customerId}");

                        cmd.ExecuteNonQuery();
                    }

                    transaction.Commit();
                    MessageBox.Show("Rental transaction saved successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);

                    RefreshSummaryPanels();
                    LoadDataGrids();
                    ResetInputFields();
                    ReturnToDashboard();
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    MessageBox.Show("Failed to save transaction: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            ReturnToDashboard();
        }

        private void ResetInputFields()
        {
            cmbCustomerName.SelectedIndex = -1;
            cmbItem.SelectedIndex = -1;
            if (numQuantity != null) numQuantity.Value = 1;
            if (numDeposit != null) numDeposit.Value = 0;
            txtNotes.Text = string.Empty;
            rbCash.Checked = true;

            dtpStartDate.Value = DateTime.Now;
            dtpStartTime.Value = DateTime.Now;
            dtpExpectedReturnDate.Value = DateTime.Now;
            dtpExpectedReturnTime.Value = DateTime.Now.AddDays(1);

            txtDailyRate.Text = "0.00";
            txtSubTotal.Text = "0.00";
            txtAmountPaid.Text = "0.00";
            txtBalanceDue.Text = "0.00";
            currentCustomerId = -1;
        }

        private void ReturnToDashboard()
        {
            if (pbUserImage != null && pbUserImage.Image != null)
            {
                pbUserImage.Image.Dispose();
                pbUserImage.Image = null;
            }

            this.Hide();
            DashBoard1 dashboard = new DashBoard1(this.currentLoggedInUserId);
            dashboard.Show();
            this.Dispose();
        }

        private void btnHome_Click(object sender, EventArgs e)
        {
            ReturnToDashboard();
        }

        private void btnCalendar_Click(object sender, EventArgs e)
        {
            if (pbUserImage != null && pbUserImage.Image != null)
            {
                pbUserImage.Image.Dispose();
                pbUserImage.Image = null;
            }

            this.Hide();
            Calendar calendarForm = new Calendar(this.currentLoggedInUserId);
            calendarForm.Show();
            this.Dispose();
        }

        private void btnInventoryManagement_Click(object sender, EventArgs e)
        {
            if (pbUserImage != null && pbUserImage.Image != null)
            {
                pbUserImage.Image.Dispose();
                pbUserImage.Image = null;
            }
            this.Hide();
            Inventory_Management inventoryForm = new Inventory_Management(this.currentLoggedInUserId);
            inventoryForm.Show();
            this.Dispose();
        }
    }
}
