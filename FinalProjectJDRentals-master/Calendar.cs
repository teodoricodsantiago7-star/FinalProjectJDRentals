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
        private DateTime currentCalendarMonthStart;

        public Calendar(int loggedInUserId)
        {
            InitializeComponent();
            this.currentLoggedInUserId = loggedInUserId > 0 ? loggedInUserId : 1;
            currentCalendarMonthStart = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);

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
                dataGridView1.Columns.Add("col" + days[i], days[i]);
            }

            dataGridView1.CellClick -= DataGridView1_CellClick;
            dataGridView1.CellClick += DataGridView1_CellClick;
        }

        private void PopulateWeeklyScheduleGrid()
        {
            if (dataGridView1 == null) return;
            dataGridView1.Rows.Clear();

            DateTime firstDayOfMonth = currentCalendarMonthStart;
            int daysInMonth = DateTime.DaysInMonth(firstDayOfMonth.Year, firstDayOfMonth.Month);
            int dayOfWeekOffset = (int)firstDayOfMonth.DayOfWeek;

            int totalCellsNeeded = daysInMonth + dayOfWeekOffset;
            int totalRowsNeeded = (int)Math.Ceiling((double)totalCellsNeeded / 7);

            DateTime calendarGridStart = firstDayOfMonth.AddDays(-dayOfWeekOffset);

            for (int r = 0; r < totalRowsNeeded; r++)
            {
                int rowIndex = dataGridView1.Rows.Add();
                dataGridView1.Rows[rowIndex].Height = 90;

                for (int c = 0; c < 7; c++)
                {
                    DateTime targetDate = calendarGridStart.AddDays((r * 7) + c);
                    DataGridViewCell cell = dataGridView1.Rows[rowIndex].Cells[c];
                    cell.Style.BackColor = Color.White;

                    if (targetDate.Month == firstDayOfMonth.Month)
                    {
                        cell.Value = $"[{targetDate.Day}]\nNo Bookings";
                        cell.Style.ForeColor = Color.DarkGray;
                        cell.Tag = targetDate.Date;
                    }
                    else
                    {
                        cell.Value = "";
                        cell.Style.BackColor = Color.WhiteSmoke;
                        cell.Tag = null;
                    }
                }
            }

            DateTime monthEnd = firstDayOfMonth.AddMonths(1);
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
                WHERE CAST(t.RentalStartDate AS DATE) < CAST(@MonthEnd AS DATE) 
                  AND CAST(t.ExpectedReturnDate AS DATE) >= CAST(@MonthStart AS DATE);";

            Dictionary<DateTime, int> bookingCounts = new Dictionary<DateTime, int>();

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                using (SqlCommand cmd = new SqlCommand(scheduleQuery, conn))
                {
                    cmd.Parameters.AddWithValue("@MonthStart", firstDayOfMonth.Date);
                    cmd.Parameters.AddWithValue("@MonthEnd", monthEnd.Date);

                    try
                    {
                        conn.Open();
                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                DateTime start = Convert.ToDateTime(reader["RentalStartDate"]).Date;
                                if (bookingCounts.ContainsKey(start))
                                {
                                    bookingCounts[start]++;
                                }
                                else
                                {
                                    bookingCounts[start] = 1;
                                }
                            }
                        }

                        for (int r = 0; r < dataGridView1.Rows.Count; r++)
                        {
                            for (int c = 0; c < 7; c++)
                            {
                                DataGridViewCell cell = dataGridView1.Rows[r].Cells[c];
                                if (cell.Tag != null)
                                {
                                    DateTime cellDate = (DateTime)cell.Tag;
                                    if (bookingCounts.ContainsKey(cellDate))
                                    {
                                        int count = bookingCounts[cellDate];
                                        string bookingWord = count == 1 ? "Booking" : "Bookings";
                                        cell.Value = $"[{cellDate.Day}]\n{count} {bookingWord}";
                                        cell.Style.ForeColor = Color.Black;
                                    }
                                }
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

        private void DataGridView1_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0 || richTextBox1 == null) return;

            DataGridViewCell cell = dataGridView1.Rows[e.RowIndex].Cells[e.ColumnIndex];
            if (cell.Tag == null)
            {
                richTextBox1.Clear();
                return;
            }

            DateTime selectedDate = (DateTime)cell.Tag;
            DisplayBookingsInRichTextBox(selectedDate);
        }

        private void DisplayBookingsInRichTextBox(DateTime selectedDate)
        {
            if (richTextBox1 == null) return;
            richTextBox1.Clear();

            string dayQuery = @"
                SELECT 
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
                WHERE CAST(t.RentalStartDate AS DATE) = @SelectedDate;";

            using (SqlConnection conn = new SqlConnection(connectionString))
            using (SqlCommand cmd = new SqlCommand(dayQuery, conn))
            {
                cmd.Parameters.AddWithValue("@SelectedDate", selectedDate.Date);
                try
                {
                    conn.Open();
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        bool hasBookings = false;
                        while (reader.Read())
                        {
                            hasBookings = true;
                            string customer = reader["CustomerName"].ToString();
                            string item = reader["ItemName"].ToString();
                            string qty = reader["Quantity"].ToString();
                            string status = reader["Status"].ToString();
                            DateTime start = Convert.ToDateTime(reader["RentalStartDate"]);
                            DateTime end = Convert.ToDateTime(reader["ExpectedReturnDate"]);

                            Color itemColor = Color.Black;
                            switch (status.Trim())
                            {
                                case "Confirmed": itemColor = Color.Green; break;
                                case "Pending": itemColor = Color.Goldenrod; break;
                                case "Overdue": itemColor = Color.Red; break;
                                case "Cancelled": itemColor = Color.Gray; break; break;
                            }

                            AppendColoredText("Customer: ", Color.Black, true);
                            AppendColoredText($"{customer}\n", Color.Black, false);

                            AppendColoredText("Item: ", Color.Black, true);
                            AppendColoredText($"{item} ({qty}x)\n", itemColor, false);

                            AppendColoredText("Start: ", Color.Black, true);
                            AppendColoredText($"{start:MM/dd hh:mm tt}\n", Color.Black, false);

                            AppendColoredText("Until: ", Color.Black, true);
                            AppendColoredText($"{end:MM/dd hh:mm tt}\n", Color.Black, false);

                            AppendColoredText(new string('-', 30) + "\n", Color.LightGray, false);
                        }

                        if (!hasBookings)
                        {
                            AppendColoredText("No Bookings for this day.", Color.DarkGray, false);
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error loading day details: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void AppendColoredText(string text, Color color, bool isBold)
        {
            if (richTextBox1 == null) return;

            richTextBox1.SelectionStart = richTextBox1.TextLength;
            richTextBox1.SelectionLength = 0;
            richTextBox1.SelectionColor = color;

            if (isBold)
                richTextBox1.SelectionFont = new Font(richTextBox1.Font, FontStyle.Bold);
            else
                richTextBox1.SelectionFont = new Font(richTextBox1.Font, FontStyle.Regular);

            richTextBox1.AppendText(text);
            richTextBox1.SelectionFont = richTextBox1.Font;
        }

        private void LoadRightPanelMetrics()
        {
            string metricsQuery = @"
                SELECT 
                    (SELECT COUNT(*) FROM RentalTransactions WHERE Status = 'Ongoing') AS ActiveRentals,
                    (SELECT COUNT(*) FROM RentalTransactions WHERE Status = 'Pending') AS NewBookings,
                    (SELECT COUNT(*) FROM Items WHERE AvailableQuantity <= 2 AND Status <> 'Discontinued') AS InventoryAlerts;";

            using (SqlConnection conn = new SqlConnection(connectionString))
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

        private void LoadUserProfilePicture()
        {
            string query = "SELECT ImagePath, FullName FROM Users WHERE UserID = @UserID;";
            using (SqlConnection conn = new SqlConnection(connectionString))
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
                                    else { pbUserProfilePic.Image = null; }
                                }
                                else { pbUserProfilePic.Image = null; }
                            }
                        }
                    }
                }
                catch { if (pbUserProfilePic != null) pbUserProfilePic.Image = null; }
            }
        }

        private void btnToday_Click(object sender, EventArgs e)
        {
            currentCalendarMonthStart = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
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
                    DataGridViewCell cell = dataGridView1.Rows[i].Cells[j];
                    if (cell.Tag != null && cell.Value != null && !cell.Value.ToString().Contains("No Bookings") && !cell.Value.ToString().ToLower().Contains(itemName.ToLower()))
                    {
                        DateTime date = (DateTime)cell.Tag;
                        cell.Value = $"[{date.Day}]\nNo Bookings";
                        cell.Style.BackColor = Color.White;
                        cell.Style.ForeColor = Color.DarkGray;
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

        private void btnRecords_Click(object sender, EventArgs e)
        {
            if (pbUserProfilePic != null && pbUserProfilePic.Image != null)
            {
                pbUserProfilePic.Image.Dispose();
                pbUserProfilePic.Image = null;
            }
            this.FormClosed -= (s, a) => Application.Exit();
            Customer_Records recordsForm = new Customer_Records(this.currentLoggedInUserId);
            recordsForm.FormClosed += (s, a) => Application.Exit();
            this.Hide();
            recordsForm.Show();
            this.Dispose();
        }

        private void btnBookingManagement_Click(object sender, EventArgs e)
        {
            if (pbUserProfilePic != null && pbUserProfilePic.Image != null)
            {
                pbUserProfilePic.Image.Dispose();
                pbUserProfilePic.Image = null;
            }
            this.FormClosed -= (s, a) => Application.Exit();
            Booking_Management bookingForm = new Booking_Management(this.currentLoggedInUserId);
            bookingForm.FormClosed += (s, a) => Application.Exit();
            this.Hide();
            bookingForm.Show();
            this.Dispose();
        }
    }
}
