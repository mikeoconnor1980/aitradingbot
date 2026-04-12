#r "nuget: Microsoft.Data.Sqlite, 9.0.0"
using Microsoft.Data.Sqlite;

var conn = new SqliteConnection("Data Source=../src/TradingApp.Api/Data/tradingapp.db");
conn.Open();

// List all tables
var cmd = conn.CreateCommand();
cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' ORDER BY name";
var reader = cmd.ExecuteReader();
Console.WriteLine("=== TABLES ===");
while (reader.Read()) Console.WriteLine(reader[0]);
reader.Close();
