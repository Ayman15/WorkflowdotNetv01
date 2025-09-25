using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;

namespace AF_SCADA_ComparisonLib
{
    public class Read_MOWT_MSSQL
    {
        public List<string> SQL_list = new List<String>();
        public Dictionary<string, object[]> SQL_dict = new Dictionary<string, object[]>();

        // Modify connection string to trust the certificate
        private static string connectionString = "Server=MXOWT-AUX; Database=scadadbR; Integrated Security=True; TrustServerCertificate=True;";
        private SqlConnection connection = new SqlConnection(connectionString);
        private string query = "SELECT * FROM scadadbR.dbo.wellname_type_table order by AvocetWellName";

        public Dictionary<string, object[]> SQL_Reader()
        {
            // Open the connection
            connection = new SqlConnection(connectionString);
            query = "SELECT * FROM scadadbR.dbo.wellname_type_table order by AvocetWellName";

            try
            {
                connection.Open();
                Console.WriteLine("Connection opened successfully.");

                SqlCommand command = new SqlCommand(query, connection);
                SqlDataReader SQL_reader = command.ExecuteReader();

                // Read the data from SQL and store it
                while (SQL_reader.Read())
                {
                    SQL_list.Add($"{SQL_reader["AvocetWellName"]}");
                    string Key = $"{SQL_reader["AvocetWellName"]}";
                    if (!SQL_dict.ContainsKey(Key))
                    {
                        object[] SCADAcolumns = new object[] {
                            SQL_reader["WellType"],
                            SQL_reader["ScadaWellName"]
                        };
                        SQL_dict.Add($"{SQL_reader["AvocetWellName"]}", SCADAcolumns);
                    }
                    else
                    {
                        Console.WriteLine(" Avocet Well Name {0} already exists.", Key);
                    }
                }

                // Output the dictionary to the console for debugging
                foreach (var item in SQL_dict)
                {
                    Console.WriteLine($"{item.Key}: {item.Value[0]}: {item.Value[1]}");
                }
            }
            catch (SqlException ex)
            {
                
                Console.WriteLine("An error occurred while reading from SQL: " + ex.Message);
            }
            finally
            {
                connection.Close();
            }

            return SQL_dict;
        }
    }
}
