using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace FinalProject
{
    public partial class Inventory_Management : Form
    {
        private readonly string connectionString = @"Server=localhost\SQLEXPRESS;Database=FinalProjectJDRENTALS;Trusted_Connection=True;";
        private int currentLoggedInUserId;

        public Inventory_Management(int loggedInUserId)
        {
            InitializeComponent();
            this.currentLoggedInUserId = loggedInUserId > 0 ? loggedInUserId : 1;

            ConfigureInventoryGrid();
            RefreshInventoryData();
            LoadUserProfilePicture();
        }

        private void Inventory_Management_Load(object sender, EventArgs e)
        {
            ConfigureInventoryGrid();
            RefreshInventoryData();
            LoadUserProfilePicture();
        }

        private void ConfigureInventoryGrid()
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
                Name = "ItemID",
                HeaderText = "ID",
                DataPropertyName = "ItemID",
                ReadOnly = true,
                Visible = true
            });

            dataGridView1.Columns.Add(new DataGridViewTextBoxColumn { Name = "ItemName", HeaderText = "Item Name", DataPropertyName = "ItemName", ReadOnly = true });
            dataGridView1.Columns.Add(new DataGridViewTextBoxColumn { Name = "TotalQuantity", HeaderText = "Total Qty", DataPropertyName = "TotalQuantity", ReadOnly = true });
            dataGridView1.Columns.Add(new DataGridViewTextBoxColumn { Name = "AvailableQuantity", HeaderText = "Available", DataPropertyName = "AvailableQuantity", ReadOnly = true });

            var rateCol = new DataGridViewTextBoxColumn { Name = "DailyRate", HeaderText = "Daily Rate", DataPropertyName = "DailyRate", ReadOnly = true };
            rateCol.DefaultCellStyle.Format = "₱#,##0.00";
            dataGridView1.Columns.Add(rateCol);

            dataGridView1.Columns.Add(new DataGridViewTextBoxColumn { Name = "Status", HeaderText = "Status", DataPropertyName = "Status", ReadOnly = true });

            var actionButtonCol = new DataGridViewButtonColumn
            {
                Name = "Action",
                HeaderText = "Action",
                Text = "Toggle Status",
                UseColumnTextForButtonValue = true
            };
            dataGridView1.Columns.Add(actionButtonCol);

            dataGridView1.CellContentClick += DataGridView1_CellContentClick;
        }

        private void RefreshInventoryData()
        {
            if (dataGridView1 == null) return;

            string query = "SELECT ItemID, ItemName, TotalQuantity, AvailableQuantity, DailyRate, Status FROM Items ORDER BY ItemName ASC;";
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

                        ApplyRowColoringRules();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Failed to load inventory data: " + ex.Message, "Database Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void ApplyRowColoringRules()
        {
            foreach (DataGridViewRow row in dataGridView1.Rows)
            {
                if (row.Cells["Status"].Value == null) continue;
                string status = row.Cells["Status"].Value.ToString();

                if (status == "Maintenance")
                {
                    row.DefaultCellStyle.BackColor = Color.Bisque;
                }
                else if (status == "Fully Booked")
                {
                    row.DefaultCellStyle.BackColor = Color.MistyRose;
                }
                else if (status == "Discontinued")
                {
                    row.DefaultCellStyle.BackColor = Color.LightGray;
                    row.DefaultCellStyle.ForeColor = Color.DarkGray;
                }
                else
                {
                    row.DefaultCellStyle.BackColor = Color.White;
                    row.DefaultCellStyle.ForeColor = Color.Black;
                }
            }
        }

        private void DataGridView1_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex != dataGridView1.Columns["Action"].Index) return;

            int itemId = Convert.ToInt32(dataGridView1.Rows[e.RowIndex].Cells["ItemID"].Value);
            string currentStatus = dataGridView1.Rows[e.RowIndex].Cells["Status"].Value.ToString();
            string newStatus = currentStatus == "Available" ? "Maintenance" : "Available";

            string updateQuery = "UPDATE Items SET Status = @NewStatus WHERE ItemID = @ItemID;";
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                using (SqlCommand cmd = new SqlCommand(updateQuery, conn))
                {
                    cmd.Parameters.AddWithValue("@NewStatus", newStatus);
                    cmd.Parameters.AddWithValue("@ItemID", itemId);

                    try
                    {
                        conn.Open();
                        cmd.ExecuteNonQuery();
                        RefreshInventoryData();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Failed to alter item status: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private bool IsItemNameDuplicate(string itemName, int excludeItemId = -1)
        {
            string query = "SELECT COUNT(*) FROM Items WHERE ItemName = @ItemName AND ItemID <> @ExcludeItemID;";
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@ItemName", itemName);
                    cmd.Parameters.AddWithValue("@ExcludeItemID", excludeItemId);
                    try
                    {
                        conn.Open();
                        int count = Convert.ToInt32(cmd.ExecuteScalar());
                        return count > 0;
                    }
                    catch
                    {
                        return false;
                    }
                }
            }
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

                                if (pbUserProfilePic != null)
                                {
                                    if (reader["ImagePath"] != DBNull.Value)
                                    {
                                        string path = reader["ImagePath"].ToString();
                                        if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                                        {
                                            pbUserProfilePic.Image?.Dispose();
                                            byte[] bytes = File.ReadAllBytes(path);
                                            using (MemoryStream ms = new MemoryStream(bytes))
                                            {
                                                pbUserProfilePic.Image = Image.FromStream(ms);
                                            }
                                            pbUserProfilePic.SizeMode = PictureBoxSizeMode.Zoom;
                                        }
                                        else
                                        {
                                            pbUserProfilePic.Image = null;
                                        }
                                    }
                                    else
                                    {
                                        pbUserProfilePic.Image = null;
                                    }
                                }
                            }
                        }
                    }
                    catch
                    {
                        if (pbUserProfilePic != null) pbUserProfilePic.Image = null;
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

        private void btnRefresh_Click(object sender, EventArgs e)
        {
            RefreshInventoryData();
        }

        private void btnAddNewItem_Click(object sender, EventArgs e)
        {
            Form namePrompt = new Form() { Width = 400, Height = 150, FormBorderStyle = FormBorderStyle.FixedDialog, Text = "Add Item", StartPosition = FormStartPosition.CenterParent, MaximizeBox = false, MinimizeBox = false };
            Label lblName = new Label() { Left = 20, Top = 20, Width = 350, Text = "Enter item name:" };
            TextBox txtName = new TextBox() { Left = 20, Top = 45, Width = 340 };
            Button btnNameOk = new Button() { Text = "OK", Left = 280, Width = 80, Top = 80, DialogResult = DialogResult.OK };
            namePrompt.Controls.Add(lblName); namePrompt.Controls.Add(txtName); namePrompt.Controls.Add(btnNameOk);
            namePrompt.AcceptButton = btnNameOk;

            if (namePrompt.ShowDialog() != DialogResult.OK) return;
            string itemName = txtName.Text.Trim();

            if (string.IsNullOrWhiteSpace(itemName))
            {
                MessageBox.Show("Please enter an item name.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (itemName.Length > 100)
            {
                MessageBox.Show("Item name is too long (max 100 letters).", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (IsItemNameDuplicate(itemName))
            {
                MessageBox.Show("This item name already exists.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            Form ratePrompt = new Form() { Width = 400, Height = 150, FormBorderStyle = FormBorderStyle.FixedDialog, Text = "Add Item", StartPosition = FormStartPosition.CenterParent, MaximizeBox = false, MinimizeBox = false };
            Label lblRate = new Label() { Left = 20, Top = 20, Width = 350, Text = "Enter daily rate (₱):" };
            TextBox txtRate = new TextBox() { Left = 20, Top = 45, Width = 340, Text = "0.00" };
            Button btnRateOk = new Button() { Text = "OK", Left = 280, Width = 80, Top = 80, DialogResult = DialogResult.OK };
            ratePrompt.Controls.Add(lblRate); ratePrompt.Controls.Add(txtRate); ratePrompt.Controls.Add(btnRateOk);
            ratePrompt.AcceptButton = btnRateOk;

            if (ratePrompt.ShowDialog() != DialogResult.OK) return;

            if (!decimal.TryParse(txtRate.Text.Trim(), out decimal dailyRate) || dailyRate < 0)
            {
                MessageBox.Show("Please enter a valid price.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (dailyRate > 1000000)
            {
                MessageBox.Show("Price is too high.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            Form qtyPrompt = new Form() { Width = 400, Height = 150, FormBorderStyle = FormBorderStyle.FixedDialog, Text = "Add Item", StartPosition = FormStartPosition.CenterParent, MaximizeBox = false, MinimizeBox = false };
            Label lblQty = new Label() { Left = 20, Top = 20, Width = 350, Text = "Enter stock quantity:" };
            TextBox txtQty = new TextBox() { Left = 20, Top = 45, Width = 340, Text = "1" };
            Button btnQtyOk = new Button() { Text = "OK", Left = 280, Width = 80, Top = 80, DialogResult = DialogResult.OK };
            qtyPrompt.Controls.Add(lblQty); qtyPrompt.Controls.Add(txtQty); qtyPrompt.Controls.Add(btnQtyOk);
            qtyPrompt.AcceptButton = btnQtyOk;

            if (qtyPrompt.ShowDialog() != DialogResult.OK) return;

            if (!int.TryParse(txtQty.Text.Trim(), out int quantity) || quantity <= 0)
            {
                MessageBox.Show("Please enter a valid quantity (1 or more).", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string insertQuery = @"
                INSERT INTO Items (ItemName, TotalQuantity, AvailableQuantity, DailyRate, Status, LastUpdated)
                VALUES (@ItemName, @Qty, @Qty, @Rate, 'Available', GETDATE());";

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                using (SqlCommand cmd = new SqlCommand(insertQuery, conn))
                {
                    cmd.Parameters.AddWithValue("@ItemName", itemName);
                    cmd.Parameters.AddWithValue("@Qty", quantity);
                    cmd.Parameters.AddWithValue("@Rate", dailyRate);

                    try
                    {
                        conn.Open();
                        cmd.ExecuteNonQuery();
                        MessageBox.Show("Item added successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        RefreshInventoryData();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Failed to save item line: " + ex.Message, "Execution Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void btnEditSelected_Click(object sender, EventArgs e)
        {
            if (dataGridView1 == null || dataGridView1.SelectedRows.Count == 0)
            {
                MessageBox.Show("Please select an item from the table first.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            int itemId = Convert.ToInt32(dataGridView1.SelectedRows[0].Cells["ItemID"].Value);
            string oldName = dataGridView1.SelectedRows[0].Cells["ItemName"].Value.ToString();
            int oldTotalQty = Convert.ToInt32(dataGridView1.SelectedRows[0].Cells["TotalQuantity"].Value);
            int oldAvailQty = Convert.ToInt32(dataGridView1.SelectedRows[0].Cells["AvailableQuantity"].Value);
            decimal oldRate = Convert.ToDecimal(dataGridView1.SelectedRows[0].Cells["DailyRate"].Value);

            Form namePrompt = new Form() { Width = 400, Height = 150, FormBorderStyle = FormBorderStyle.FixedDialog, Text = "Edit Item", StartPosition = FormStartPosition.CenterParent, MaximizeBox = false, MinimizeBox = false };
            Label lblName = new Label() { Left = 20, Top = 20, Width = 350, Text = "Change item name:" };
            TextBox txtName = new TextBox() { Left = 20, Top = 45, Width = 340, Text = oldName };
            Button btnNameOk = new Button() { Text = "OK", Left = 280, Width = 80, Top = 80, DialogResult = DialogResult.OK };
            namePrompt.Controls.Add(lblName); namePrompt.Controls.Add(txtName); namePrompt.Controls.Add(btnNameOk);
            namePrompt.AcceptButton = btnNameOk;

            if (namePrompt.ShowDialog() != DialogResult.OK) return;
            string newName = txtName.Text.Trim();

            if (string.IsNullOrWhiteSpace(newName))
            {
                MessageBox.Show("Item name cannot be empty.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (newName.Length > 100)
            {
                MessageBox.Show("Item name is too long (max 100 letters).", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (IsItemNameDuplicate(newName, itemId))
            {
                MessageBox.Show("This item name already exists.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            Form ratePrompt = new Form() { Width = 400, Height = 150, FormBorderStyle = FormBorderStyle.FixedDialog, Text = "Edit Item", StartPosition = FormStartPosition.CenterParent, MaximizeBox = false, MinimizeBox = false };
            Label lblRate = new Label() { Left = 20, Top = 20, Width = 350, Text = "Change daily rate (₱):" };
            TextBox txtRate = new TextBox() { Left = 20, Top = 45, Width = 340, Text = oldRate.ToString("F2") };
            Button btnRateOk = new Button() { Text = "OK", Left = 280, Width = 80, Top = 80, DialogResult = DialogResult.OK };
            ratePrompt.Controls.Add(lblRate); ratePrompt.Controls.Add(txtRate); ratePrompt.Controls.Add(btnRateOk);
            ratePrompt.AcceptButton = btnRateOk;

            if (ratePrompt.ShowDialog() != DialogResult.OK) return;

            if (!decimal.TryParse(txtRate.Text.Trim(), out decimal newRate) || newRate < 0)
            {
                MessageBox.Show("Please enter a valid price.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            Form adjustQtyPrompt = new Form() { Width = 420, Height = 170, FormBorderStyle = FormBorderStyle.FixedDialog, Text = "Adjust Stock", StartPosition = FormStartPosition.CenterParent, MaximizeBox = false, MinimizeBox = false };
            Label lblAdjust = new Label() { Left = 20, Top = 15, Width = 380, Height = 40, Text = "Add or remove stock units:\n(Example: +5 to add stock, -2 to reduce stock)" };
            TextBox txtAdjust = new TextBox() { Left = 20, Top = 60, Width = 360, Text = "0" };
            Button btnAdjustOk = new Button() { Text = "OK", Left = 300, Width = 80, Top = 100, DialogResult = DialogResult.OK };
            adjustQtyPrompt.Controls.Add(lblAdjust); adjustQtyPrompt.Controls.Add(txtAdjust); adjustQtyPrompt.Controls.Add(btnAdjustOk);
            adjustQtyPrompt.AcceptButton = btnAdjustOk;

            if (adjustQtyPrompt.ShowDialog() != DialogResult.OK) return;

            string rawInput = txtAdjust.Text.Trim();
            bool isNegative = false;

            if (rawInput.StartsWith("-"))
            {
                isNegative = true;
                rawInput = rawInput.Substring(1).Trim();
            }
            else if (rawInput.StartsWith("+"))
            {
                rawInput = rawInput.Substring(1).Trim();
            }

            if (!int.TryParse(rawInput, out int adjustmentQty) || adjustmentQty < 0)
            {
                MessageBox.Show("Please enter a valid whole number.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (isNegative)
            {
                adjustmentQty = -adjustmentQty;
            }

            int finalTotalQty = oldTotalQty + adjustmentQty;
            int finalAvailQty = oldAvailQty + adjustmentQty;

            if (finalTotalQty < 0 || finalAvailQty < 0)
            {
                MessageBox.Show("Stock quantity cannot go below 0.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            string finalStatus = finalAvailQty <= 0 ? "Fully Booked" : "Available";

            string updateQuery = @"
                UPDATE Items 
                SET ItemName = @ItemName, 
                    DailyRate = @DailyRate, 
                    TotalQuantity = @TotalQty, 
                    AvailableQuantity = @AvailQty,
                    Status = CASE WHEN Status <> 'Maintenance' AND Status <> 'Discontinued' THEN @Status ELSE Status END
                WHERE ItemID = @ItemID;";

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                using (SqlCommand cmd = new SqlCommand(updateQuery, conn))
                {
                    cmd.Parameters.AddWithValue("@ItemName", newName);
                    cmd.Parameters.AddWithValue("@DailyRate", newRate);
                    cmd.Parameters.AddWithValue("@TotalQty", finalTotalQty);
                    cmd.Parameters.AddWithValue("@AvailQty", finalAvailQty);
                    cmd.Parameters.AddWithValue("@Status", finalStatus);
                    cmd.Parameters.AddWithValue("@ItemID", itemId);

                    try
                    {
                        conn.Open();
                        cmd.ExecuteNonQuery();
                        MessageBox.Show("Item updated successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        RefreshInventoryData();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Failed to execute update sequence: " + ex.Message, "Execution Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void SafelyNavigateToForm(Form targetForm)
        {
            if (pbUserProfilePic != null && pbUserProfilePic.Image != null)
            {
                pbUserProfilePic.Image.Dispose();
                pbUserProfilePic.Image = null;
            }
            this.Hide();
            targetForm.Show();
            this.Dispose();
        }
    }
}
