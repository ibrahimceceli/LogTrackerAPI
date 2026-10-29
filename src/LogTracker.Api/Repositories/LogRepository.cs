using Microsoft.Data.SqlClient;
using System.Data;
using LogTrackerAPI.Models;

namespace LogTrackerAPI.Repositories
{
    public class LogRepository
    {
        private readonly string _connectionString;

        // Constructor (Kurucu Metot) ile bağlantı cümlesini appsettings.json'dan çekiyoruz
        public LogRepository(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection");
        }

        /// <summary>
        /// SQL Server'daki sp_InsertErrorLog prosedürünü çağırır.
        /// </summary>
        public void LogError(string errorMessage, string errorSource, string errorType)
        {
            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                using (SqlCommand command = new SqlCommand("sp_InsertErrorLog", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;

                    // C# null değerlerini SQL'in anlayacağı DBNull.Value değerine çeviriyoruz
                    command.Parameters.AddWithValue("@ErrorMessage", (object)errorMessage ?? DBNull.Value);
                    command.Parameters.AddWithValue("@ErrorSource", (object)errorSource ?? DBNull.Value);
                    command.Parameters.AddWithValue("@ErrorType", (object)errorType ?? DBNull.Value);

                    connection.Open();
                    command.ExecuteNonQuery();
                }
            }
        }

        /// <summary>
        /// SQL Server'daki sp_InsertPerformanceMetric prosedürünü çağırır.
        /// </summary>
        public void LogPerformance(string endpointPath, int responseTimeMS)
        {
            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                using (SqlCommand command = new SqlCommand("sp_InsertPerformanceMetric", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;

                    command.Parameters.AddWithValue("@EndpointPath", (object)endpointPath ?? DBNull.Value);
                    command.Parameters.AddWithValue("@ResponseTimeMS", responseTimeMS);

                    connection.Open();
                    command.ExecuteNonQuery();
                }
            }
        }

        /// <summary>
        /// SQL Server'dan en güncel hata loglarını getirir.
        /// </summary>
        public List<ErrorLog> GetErrorLogs()
        {
            var logs = new List<ErrorLog>();

            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                // En yeni hataları en üstte görmek için tarihe göre azalan (DESC) sıralıyoruz
                string query = "SELECT ErrorID, ErrorMessage, ErrorSource, ErrorType, LogDate FROM ErrorLogs ORDER BY LogDate DESC";

                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    connection.Open();
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            logs.Add(new ErrorLog
                            {
                                ErrorID = Convert.ToInt32(reader["ErrorID"]),
                                ErrorMessage = reader["ErrorMessage"].ToString(),
                                ErrorSource = reader["ErrorSource"].ToString(),
                                ErrorType = reader["ErrorType"].ToString(),
                                LogDate = Convert.ToDateTime(reader["LogDate"])
                            });
                        }
                    }
                }
            }
            return logs;
        }

        /// <summary>
        /// SQL Server'dan en güncel performans metriklerini getirir.
        /// </summary>
        public List<PerformanceMetric> GetPerformanceMetrics()
        {
            var metrics = new List<PerformanceMetric>();

            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                string query = "SELECT MetricID, EndpointPath, ResponseTimeMS, LogDate FROM PerformanceMetrics ORDER BY LogDate DESC";

                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    connection.Open();
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            metrics.Add(new PerformanceMetric
                            {
                                MetricID = Convert.ToInt32(reader["MetricID"]),
                                EndpointPath = reader["EndpointPath"].ToString(),
                                ResponseTimeMS = Convert.ToInt32(reader["ResponseTimeMS"]),
                                LogDate = Convert.ToDateTime(reader["LogDate"])
                            });
                        }
                    }
                }
            }
            return metrics;
        }

        /// <summary>
        /// Her endpoint'in kaç kez çağrıldığını ve ortalama yanıt süresini (MS) döner.
        /// </summary>
        public List<object> GetPerformanceAnalysis()
        {
            var analysis = new List<object>();

            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                // SQL'in GROUP BY gücünü kullanarak ortalamaları (AVG) ve toplam istek sayılarını (COUNT) hesaplıyoruz
                string query = @"
                    SELECT 
                        EndpointPath, 
                        COUNT(*) AS TotalRequests, 
                        AVG(ResponseTimeMS) AS AverageResponseTimeMS 
                    FROM PerformanceMetrics 
                    GROUP BY EndpointPath";

                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    connection.Open();
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            analysis.Add(new
                            {
                                Endpoint = reader["EndpointPath"].ToString(),
                                TotalRequests = Convert.ToInt32(reader["TotalRequests"]),
                                AverageResponseTime = Convert.ToInt32(reader["AverageResponseTimeMS"])
                            });
                        }
                    }
                }
            }
            return analysis;
        }

        /// <summary>
        /// SQL Server'daki sp_GetHourlyErrorMatrix prosedürünü çalıştırarak saatlik hata dağılımını getirir.
        /// </summary>
        public List<HourlyErrorMatrix> GetHourlyErrorMatrix()
        {
            var matrix = new List<HourlyErrorMatrix>();

            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                using (SqlCommand command = new SqlCommand("sp_GetHourlyErrorMatrix", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    connection.Open();

                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            matrix.Add(new HourlyErrorMatrix
                            {
                                HourOfDate = Convert.ToInt32(reader["Saat Dilimi"]),
                                NullReference = Convert.ToInt32(reader["NullReference"]),
                                SqlException = Convert.ToInt32(reader["SqlException"]),
                                DivideByZeroException = Convert.ToInt32(reader["DivideByZeroException"]),
                                TimeoutException = Convert.ToInt32(reader["TimeoutException"])
                            });
                        }
                    }
                }
            }
            return matrix;
        }

        /// <summary>
        /// SQL Server'daki sp_GetWeeklyPerformanceTrend prosedürünü çalıştırarak haftalık trendi getirir.
        /// </summary>
        public List<WeeklyPerformanceTrend> GetWeeklyPerformanceTrend()
        {
            var trend = new List<WeeklyPerformanceTrend>();

            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                using (SqlCommand command = new SqlCommand("sp_GetWeeklyPerformanceTrend", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    connection.Open();

                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            trend.Add(new WeeklyPerformanceTrend
                            {
                                EndpointPath = reader["Endpoint Adresi"].ToString() ?? string.Empty,
                                Monday = Convert.ToInt32(reader["Pazartesi (ms)"]),
                                Tuesday = Convert.ToInt32(reader["Salı (ms)"]),
                                Wednesday = Convert.ToInt32(reader["Çarşamba (ms)"]),
                                Thursday = Convert.ToInt32(reader["Perşembe (ms)"]),
                                Friday = Convert.ToInt32(reader["Cuma (ms)"]),
                                Saturday = Convert.ToInt32(reader["Cumartesi (ms)"]),
                                Sunday = Convert.ToInt32(reader["Pazar (ms)"])
                            });
                        }
                    }
                }
            }
            return trend;
        }
    }
}