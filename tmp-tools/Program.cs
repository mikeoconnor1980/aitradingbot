using Microsoft.Data.Sqlite;
using System;
var db = "data\\tradingapp.db";
using var cn = new SqliteConnection($"Data Source={db}");
cn.Open();
var cmd = cn.CreateCommand();
cmd.CommandText = "DELETE FROM Candles";
var rows = cmd.ExecuteNonQuery();
Console.WriteLine($"Deleted {rows} rows from Candles");
