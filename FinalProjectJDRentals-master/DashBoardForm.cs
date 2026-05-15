using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;
using System.Data.SqlClient;
using System.IO;

namespace FinalProject
{
    public partial class DashBoard1 : Form
    {
        private readonly string connectionString = @"Server=localhost\SQLEXPRESS;Database=FinalProjectJDRENTALS;Trusted_Connection=True;";
        private int currentLoggedInUserId;

        public DashBoard1()
        {
            InitializeComponent();
            this.currentLoggedInUserId = (int)GetFallbackUserId();
            InitializeDashboardChart();
            RefreshDashboardData();
        }

        public DashBoard1(int loggedInUserId)
        {
            InitializeComponent();
            this.currentLoggedInUserId = loggedInUserId > 0 ? loggedInUserId : (int)GetFallbackUserId();
            InitializeDashboardChart();
            RefreshDashboardData();
        }

        private void DashBoard1_Load(object sender, EventArgs e)
        {
        }

        private void label21_Click(object sender, EventArgs e)
        {
            if (chart1 != null)
            {
                chart1.Series.Clear();
                chart1.Dispose();
            }
            if (pbProfilePic != null && pbProfilePic.Image != null)
            {
                pbProfilePic.Image.Dispose();
                pbProfilePic.Image = null;
            }
            this.Hide();
            LogIn logIn = new LogIn();
            logIn.Show();
            this.Close();
        }

        private void btnRefresh_Click(object sender, EventArgs e)
        {
            RefreshDashboardData();
        }

        private void btnNewRentalTransaction_Click(object sender, EventArgs e)
        {
            if (chart1 != null && chart1.Series.Count > 0)
            {
                chart1.Series["Series1"].Points.Clear();
            }

            if (pbProfilePic != null && pbProfilePic.Image != null)
            {
                pbProfilePic.Image.Dispose();
                pbProfilePic.Image = null;
            }

            this.Hide();

            NewRentalTransaction rentalForm = new NewRentalTransaction(this.currentLoggedInUserId);

            rentalForm.FormClosed += (s, args) =>
            {
                this.Close();
            };

            rentalForm.Show();
        }

        private long GetFallbackUserId()
        {
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                string query = "SELECT TOP 1 UserID FROM Users ORDER BY UserID DESC;";
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    try
                    {
                        conn.Open();
                        object res = cmd.ExecuteScalar();
                        return res != null ? Convert.ToInt64(res) : 1;
                    }
                    catch
                    {
                        return 1;
                    }
                }
            }
        }

        private void InitializeDashboardChart()
        {
            if (chart1.ChartAreas.Count == 0)
            {
                chart1.ChartAreas.Add(new ChartArea("ChartArea1"));
            }

            ChartArea area = chart1.ChartAreas["ChartArea1"];
            area.AxisX.MajorGrid.LineColor = Color.LightGray;
            area.AxisY.MajorGrid.LineColor = Color.LightGray;
            area.AxisX.Interval = 2;
            area.AxisX.Minimum = 0;
            area.AxisX.Maximum = DateTime.DaysInMonth(DateTime.Now.Year, DateTime.Now.Month) + 1;
            area.AxisY.LabelStyle.Format = "#,##0";

            area.AxisX.Title = "Day of the Month";
            area.AxisY.Title = "Total Revenue (₱)";
            area.AxisX.TitleFont = new Font("Arial", 10, FontStyle.Bold);
            area.AxisY.TitleFont = new Font("Arial", 10, FontStyle.Bold);

            chart1.Titles.Clear();
            Title chartTitle = new Title
            {
                Text = $"Daily Revenue Summary - {DateTime.Now:MMMM yyyy}",
                Font = new Font("Arial", 12, FontStyle.Bold),
                ForeColor = Color.Black
            };
            chart1.Titles.Add(chartTitle);

            chart1.Series.Clear();
            Series series = new Series("Series1")
            {
                ChartType = SeriesChartType.Column,
                Color = Color.FromArgb(139, 69, 19),
                XValueType = ChartValueType.Int32,
                YValueType = ChartValueType.Double,
                ChartArea = area.Name,
                IsValueShownAsLabel = false
            };
            chart1.Series.Add(series);

            chart1.Legends.Clear();
        }

        public void RefreshDashboardData()
        {
            try
            {
                DashboardMetrics metrics = FetchMetricsFromDatabase();
                UpdateUIFields(metrics);
                UpdateChartDisplay(metrics.DailySalesData);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Data Load Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private DashboardMetrics FetchMetricsFromDatabase()
        {
            DashboardMetrics metrics = new DashboardMetrics();

            string metricsQuery = @"
                SELECT 
                    (SELECT COUNT(*) FROM RentalTransactions WHERE CAST(RentalStartDate AS DATE) = CAST(GETDATE() AS DATE)) AS RentalsToday,
                    (SELECT COUNT(*) FROM RentalTransactions WHERE Status = 'Ongoing') AS OngoingRentals,
                    (SELECT COUNT(*) FROM RentalTransactions WHERE Status = 'Pending') AS PendingBookings,
                    (SELECT COUNT(*) FROM RentalTransactions WHERE CAST(RentalStartDate AS DATE) = CAST(DATEADD(day, 1, GETDATE()) AS DATE) AND Status = 'Confirmed') AS UpcomingTomorrow,
                    (SELECT COUNT(*) FROM RentalTransactions WHERE ExpectedReturnDate < GETDATE() AND ActualReturnDate IS NULL AND Status <> 'Cancelled') AS OverdueReturns,
                    (SELECT COUNT(*) FROM RentalDetails WHERE ConditionAfter IN ('Damaged', 'Broken') OR DamageNotes IS NOT NULL) AS DamagedItems,
                    (SELECT ISNULL(SUM(AvailableQuantity), 0) FROM Items WHERE Status = 'Available') AS AvailableItems,
                    (SELECT ImagePath FROM Users WHERE UserID = @UserID) AS UserProfilePath,
                    (SELECT FullName FROM Users WHERE UserID = @UserID) AS UserFullName;

                SELECT TOP 1 ItemName, AvailableQuantity 
                FROM Items 
                WHERE AvailableQuantity <= 2 AND Status <> 'Discontinued'
                ORDER BY AvailableQuantity ASC;

                SELECT 
                    DAY(RentalStartDate) AS DayOfMonth, 
                    SUM(TotalAmount) AS DailyTotal
                FROM RentalTransactions
                WHERE MONTH(RentalStartDate) = MONTH(GETDATE()) 
                  AND YEAR(RentalStartDate) = YEAR(GETDATE())
                  AND Status <> 'Cancelled'
                GROUP BY DAY(RentalStartDate);";

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                using (SqlCommand cmd = new SqlCommand(metricsQuery, conn))
                {
                    cmd.Parameters.AddWithValue("@UserID", currentLoggedInUserId);
                    conn.Open();
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            metrics.RentalsToday = Convert.ToInt32(reader["RentalsToday"]);
                            metrics.OngoingRentals = Convert.ToInt32(reader["OngoingRentals"]);
                            metrics.PendingBookings = Convert.ToInt32(reader["PendingBookings"]);
                            metrics.UpcomingTomorrow = Convert.ToInt32(reader["UpcomingTomorrow"]);
                            metrics.OverdueReturns = Convert.ToInt32(reader["OverdueReturns"]);
                            metrics.DamagedItemsCount = Convert.ToInt32(reader["DamagedItems"]);
                            metrics.AvailableItemsCount = Convert.ToInt32(reader["AvailableItems"]);
                            metrics.ProfileImagePath = reader["UserProfilePath"]?.ToString();
                            metrics.FullName = reader["UserFullName"]?.ToString();
                        }

                        if (reader.NextResult() && reader.Read())
                        {
                            metrics.LowStockAlertText = $"{reader["ItemName"]} ({reader["AvailableQuantity"]} left)";
                        }
                        else
                        {
                            metrics.LowStockAlertText = "All items well stocked.";
                        }

                        if (reader.NextResult())
                        {
                            while (reader.Read())
                            {
                                int day = Convert.ToInt32(reader["DayOfMonth"]);
                                double total = Convert.ToDouble(reader["DailyTotal"]);
                                metrics.DailySalesData.Add(day, total);
                            }
                        }
                    }
                }
            }
            return metrics;
        }

        private void UpdateUIFields(DashboardMetrics metrics)
        {
            if (lblRentalsTodayVal != null) lblRentalsTodayVal.Text = metrics.RentalsToday.ToString();
            if (lblOngoingRentalsVal != null) lblOngoingRentalsVal.Text = metrics.OngoingRentals.ToString();
            if (lblPendingBookingsVal != null) lblPendingBookingsVal.Text = metrics.PendingBookings.ToString();
            if (lblAvailableItemsVal != null) lblAvailableItemsVal.Text = metrics.AvailableItemsCount.ToString();

            if (lblLowStockVal != null) lblLowStockVal.Text = metrics.LowStockAlertText;
            if (lblUpcomingBookingsVal != null) lblUpcomingBookingsVal.Text = $"{metrics.UpcomingTomorrow} tomorrow";
            if (lblOverdueReturnsVal != null) lblOverdueReturnsVal.Text = $"{metrics.OverdueReturns} item" + (metrics.OverdueReturns == 1 ? "" : "s");
            if (lblDamagedItemsVal != null) lblDamagedItemsVal.Text = metrics.DamagedItemsCount.ToString();

            if (UserNameHeader != null)
            {
                UserNameHeader.Text = !string.IsNullOrWhiteSpace(metrics.FullName) ? metrics.FullName : "Staff Member";
            }

            try
            {
                if (pbProfilePic != null)
                {
                    if (!string.IsNullOrWhiteSpace(metrics.ProfileImagePath) && File.Exists(metrics.ProfileImagePath))
                    {
                        pbProfilePic.Image?.Dispose();
                        pbProfilePic.Image = Image.FromFile(metrics.ProfileImagePath);
                        pbProfilePic.SizeMode = PictureBoxSizeMode.Zoom;
                    }
                    else
                    {
                        pbProfilePic.Image = null;
                    }
                }
            }
            catch
            {
                if (pbProfilePic != null) pbProfilePic.Image = null;
            }
        }

        private void UpdateChartDisplay(Dictionary<int, double> dailyData)
        {
            if (chart1 == null) return;
            if (chart1.Series.Count == 0 || !chart1.Series.Contains(chart1.Series["Series1"])) return;

            chart1.Series["Series1"].Points.Clear();
            int daysInMonth = DateTime.DaysInMonth(DateTime.Now.Year, DateTime.Now.Month);

            for (int day = 1; day <= daysInMonth; day++)
            {
                double amount = dailyData.ContainsKey(day) ? dailyData[day] : 0.0;
                if (amount > 0)
                {
                    chart1.Series["Series1"].Points.AddXY(day, amount);
                }
            }

            chart1.Invalidate();
            chart1.Update();
        }
        private void btnCalendar_Click(object sender, EventArgs e)
        {
            if (chart1 != null && chart1.Series.Count > 0)
            {
                chart1.Series["Series1"].Points.Clear();
            }

            if (pbProfilePic != null && pbProfilePic.Image != null)
            {
                pbProfilePic.Image.Dispose();
                pbProfilePic.Image = null;
            }

            this.Hide();
            Calendar calendarForm = new Calendar(this.currentLoggedInUserId);
            calendarForm.Show();
        }

        private void btnInventoryManagement_Click(object sender, EventArgs e)
        {
            if (chart1 != null && chart1.Series.Count > 0)
            {
                chart1.Series["Series1"].Points.Clear();
            }

            if (pbProfilePic != null && pbProfilePic.Image != null)
            {
                pbProfilePic.Image.Dispose();
                pbProfilePic.Image = null;
            }
            this.Hide();
            Inventory_Management inventoryForm = new Inventory_Management(this.currentLoggedInUserId);
            inventoryForm.Show();
        }

    }

    public class DashboardMetrics
    {
        public int RentalsToday { get; set; }
        public int OngoingRentals { get; set; }
        public int PendingBookings { get; set; }
        public int UpcomingTomorrow { get; set; }
        public int OverdueReturns { get; set; }
        public int DamagedItemsCount { get; set; }
        public int AvailableItemsCount { get; set; }
        public string ProfileImagePath { get; set; }
        public string LowStockAlertText { get; set; }
        public string FullName { get; set; }
        public Dictionary<int, double> DailySalesData { get; set; } = new Dictionary<int, double>();
    }

}
