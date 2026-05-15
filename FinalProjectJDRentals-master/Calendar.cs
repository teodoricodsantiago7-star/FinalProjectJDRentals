using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace FinalProject
{
    public partial class Calendar : Form
    {
        private readonly string connectionString = @"Server=localhost\SQLEXPRESS;Database=FinalProjectJDRENTALS;Trusted_Connection=True;";
        private int currentLoggedInUserId;
        private DateTime currentCalendarWeekStart;

        private readonly Color ColorConfirmed = Color.LightGreen;
        private readonly Color ColorPending = Color.Khaki;
        private readonly Color ColorOverdue = Color.LightCoral;
        private readonly Color ColorCancelled = Color.LightGray;

        public Calendar(int loggedInUserId)
        {
            InitializeComponent();
            this.currentLoggedInUserId = loggedInUserId > 0 ? loggedInUserId : 1;
            currentCalendarWeekStart = GetStartOfWeek(DateTime.Now);

            ConfigureCalendarGridStructure();
            RefreshCalendarData();
        }

        private void Calendar_Load(object sender, EventArgs e)
        {
            ConfigureCalendarGridStructure();
            RefreshCalendarData();
        }

        private void RefreshCalendarData()
        {
            LoadRightPanelMetrics();
            PopulateWeeklyScheduleGrid();
            LoadUserProfilePicture();
        }

        private DateTime GetStartOfWeek(DateTime dt)
        {
            int diff = (7 + (dt.DayOfWeek - DayOfWeek.Sunday)) % 7;
            return dt.AddDays(-1 * diff).Date;
        }

        private void ConfigureCalendarGridStructure()
        {
            if (dataGridView1 == null) return;

            dataGridView1.Columns.Clear();
            dataGridView1.AllowUserToAddRows = false;
            dataGridView1.AllowUserToDeleteRows = false;
            dataGridView1.ReadOnly = true;
            dataGridView1.RowHeadersVisible = false;
            dataGridView1.SelectionMode = DataGridViewSelectionMode.CellSelect;
            dataGridView1.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            dataGridView1.DefaultCellStyle.WrapMode = DataGridViewTriState.True;

            string[] days = { "Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday" };
            for (int i = 0; i < 7; i++)
            {
                DateTime currentDay = currentCalendarWeekStart.AddDays(i);
                string headerText = $"{days[i]}\n({currentDay:MMM dd})";
                dataGridView1.Columns.Add("col" + days[i], headerText);
            }
        }

        private void PopulateWeeklyScheduleGrid()
        {
            if (dataGridView1 == null) return;
            dataGridView1.Rows.Clear();

            for (int row = 0; row < 3; row++)
            {
                int rowIndex = dataGridView1.Rows.Add();
                dataGridView1.Rows[rowIndex].Height = 75;
                for (int col = 0; col < 7; col++)
                {
                    dataGridView1.Rows[rowIndex].Cells[col].Value = "";
                }
            }

            DateTime weekEnd = currentCalendarWeekStart.AddDays(7);
            string scheduleQuery = @"
                SELECT 
                    t.TransactionID,
                    c.Name AS CustomerName,
                    i.ItemName,
                    rd.Quantity,
                    t.RentalStartDate,
                    t.ExpectedReturnDate,
                    t.Status
                FROM RentalTransactions t
                INNER JOIN Customers c ON t.CustomerID = c.CustomerID
                INNER JOIN RentalDetails rd ON t.TransactionID = rd.TransactionID
                INNER JOIN Items i ON rd.ItemID = i.ItemID
                WHERE CAST(t.RentalStartDate AS DATE) < CAST(@WeekEnd AS DATE) 
                  AND CAST(t.ExpectedReturnDate AS DATE) >= CAST(@WeekStart AS DATE);";

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                using (SqlCommand cmd = new SqlCommand(scheduleQuery, conn))
                {
                    cmd.Parameters.AddWithValue("@WeekStart", currentCalendarWeekStart.Date);
                    cmd.Parameters.AddWithValue("@WeekEnd", weekEnd.Date);

                    try
                    {
                        conn.Open();
                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                string txId = reader["TransactionID"].ToString();
                                string customer = reader["CustomerName"].ToString();
                                string item = reader["ItemName"].ToString();
                                string qty = reader["Quantity"].ToString();
                                string status = reader["Status"].ToString();

                                DateTime start = Convert.ToDateTime(reader["RentalStartDate"]);
                                DateTime end = Convert.ToDateTime(reader["ExpectedReturnDate"]);

                                string displayDetails = $"{item} ({qty}x)\nStart: {start:MM/dd hh:mm tt}\nExpected: {end:MM/dd hh:mm tt}";

                                int startToken = Convert.ToInt32(start.ToString("yyyyMMdd"));

                                for (int col = 0; col < 7; col++)
                                {
                                    int targetToken = Convert.ToInt32(currentCalendarWeekStart.AddDays(col).ToString("yyyyMMdd"));

                                    if (targetToken == startToken)
                                    {
                                        int targetRow = 0;
                                        while (targetRow < dataGridView1.Rows.Count &&
                                               dataGridView1.Rows[targetRow].Cells[col].Value != null &&
                                               !string.IsNullOrEmpty(dataGridView1.Rows[targetRow].Cells[col].Value.ToString()))
                                        {
                                            targetRow++;
                                        }

                                        if (targetRow >= dataGridView1.Rows.Count)
                                        {
                                            int newRowIdx = dataGridView1.Rows.Add();
                                            dataGridView1.Rows[newRowIdx].Height = 75;
                                            targetRow = newRowIdx;
                                        }

                                        DataGridViewCell targetCell = dataGridView1.Rows[targetRow].Cells[col];
                                        targetCell.Value = displayDetails;
                                        ApplyCellLegendColoring(targetCell, status);
                                    }
                                }
                            }
                        }

                        for (int col = 0; col < 7; col++)
                        {
                            bool isColumnEmpty = true;
                            for (int row = 0; row < dataGridView1.Rows.Count; row++)
                            {
                                if (dataGridView1.Rows[row].Cells[col].Value != null &&
                                    !string.IsNullOrEmpty(dataGridView1.Rows[row].Cells[col].Value.ToString()))
                                {
                                    isColumnEmpty = false;
                                    break;
                                }
                            }

                            if (isColumnEmpty && dataGridView1.Rows.Count > 0)
                            {
                                dataGridView1.Rows[0].Cells[col].Value = "No Bookings";
                                dataGridView1.Rows[0].Cells[col].Style.ForeColor = Color.DarkGray;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Could not load calendar entries: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void ApplyCellLegendColoring(DataGridViewCell cell, string status)
        {
            cell.Style.ForeColor = Color.Black;
            switch (status)
            {
                case "Confirmed":
                    cell.Style.BackColor = ColorConfirmed;
                    break;
                case "Pending":
                    cell.Style.BackColor = ColorPending;
                    break;
                case "Overdue":
                    cell.Style.BackColor = ColorOverdue;
                    break;
                case "Cancelled":
                    cell.Style.BackColor = ColorCancelled;
                    break;
                default:
                    cell.Style.BackColor = Color.White;
                    break;
            }
        }

        private void LoadRightPanelMetrics()
        {
            string metricsQuery = @"
                SELECT 
                    (SELECT COUNT(*) FROM RentalTransactions WHERE Status = 'Ongoing') AS ActiveRentals,
                    (SELECT COUNT(*) FROM RentalTransactions WHERE Status = 'Pending') AS NewBookings,
                    (SELECT COUNT(*) FROM Items WHERE AvailableQuantity <= 2 AND Status <> 'Discontinued') AS InventoryAlerts;";

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                using (SqlCommand cmd = new SqlCommand(metricsQuery, conn))
                {
                    try
                    {
                        conn.Open();
                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                if (lblActiveRentalsCount != null) lblActiveRentalsCount.Text = reader["ActiveRentals"].ToString();
                                if (lblNewBookingsCount != null) lblNewBookingsCount.Text = reader["NewBookings"].ToString();
                                if (lblInventoryAlertsCount != null) lblInventoryAlertsCount.Text = reader["InventoryAlerts"].ToString();
                            }
                        }
                    }
                    catch { }
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

        private void btnToday_Click(object sender, EventArgs e)
        {
            currentCalendarWeekStart = GetStartOfWeek(DateTime.Now);
            ConfigureCalendarGridStructure();
            RefreshCalendarData();
        }

        private void btnAddNewBooking_Click(object sender, EventArgs e)
        {
            SafelyNavigateToForm(new NewRentalTransaction(this.currentLoggedInUserId));
        }

        private void btnNewRentalTransaction_Click(object sender, EventArgs e)
        {
            SafelyNavigateToForm(new NewRentalTransaction(this.currentLoggedInUserId));
        }

        private void btnHome_Click(object sender, EventArgs e)
        {
            SafelyNavigateToForm(new DashBoard1(this.currentLoggedInUserId));
        }

        private void btnInventoryManagement_Click(object sender, EventArgs e)
        {
            SafelyNavigateToForm(new Inventory_Management(this.currentLoggedInUserId));
        }

        private void btnFilterByItem_Click(object sender, EventArgs e)
        {
            Form prompt = new Form()
            {
                Width = 400,
                Height = 150,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                Text = "Filter Schedule",
                StartPosition = FormStartPosition.CenterParent,
                MaximizeBox = false,
                MinimizeBox = false
            };

            Label textLabel = new Label() { Left = 20, Top = 20, Width = 350, Text = "Search item name:" };
            TextBox textBox = new TextBox() { Left = 20, Top = 45, Width = 340 };
            Button confirmation = new Button() { Text = "OK", Left = 280, Width = 80, Top = 80, DialogResult = DialogResult.OK };

            prompt.Controls.Add(textLabel);
            prompt.Controls.Add(textBox);
            prompt.Controls.Add(confirmation);
            prompt.AcceptButton = confirmation;

            if (prompt.ShowDialog() == DialogResult.OK)
            {
                string inputItem = textBox.Text;
                if (string.IsNullOrWhiteSpace(inputItem))
                {
                    RefreshCalendarData();
                    return;
                }
                FilterCalendarRowsByItem(inputItem.Trim());
            }
        }

        private void FilterCalendarRowsByItem(string itemName)
        {
            if (dataGridView1 == null) return;
            for (int i = 0; i < dataGridView1.Rows.Count; i++)
            {
                for (int j = 0; j < 7; j++)
                {
                    var cell = dataGridView1.Rows[i].Cells[j];
                    if (cell.Value != null && !cell.Value.ToString().Contains("No Bookings") && !cell.Value.ToString().ToLower().Contains(itemName.ToLower()))
                    {
                        cell.Value = "";
                        cell.Style.BackColor = Color.White;
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
