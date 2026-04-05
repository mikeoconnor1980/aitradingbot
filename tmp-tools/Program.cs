using Microsoft.Data.Sqlite;
using System.Text.Json;

using var conn = new SqliteConnection("Data Source=../data/tradingapp.db");
conn.Open();

// Check latest optimization runs
Console.WriteLine("=== Recent Optimization Runs ===");
var cmd = conn.CreateCommand();
cmd.CommandText = "SELECT Id, Symbol, Status, TotalCombinations, CompletedCount, QualifiedCount, FailedCount, ElapsedMs, ErrorMessage FROM OptimizationRuns ORDER BY CreatedAtUtc DESC LIMIT 5";
using var reader = cmd.ExecuteReader();
while (reader.Read())
{
    Console.WriteLine($"ID: {reader["Id"]}");
    Console.WriteLine($"  Symbol: {reader["Symbol"]}, Status: {reader["Status"]}");
    Console.WriteLine($"  Total: {reader["TotalCombinations"]}, Completed: {reader["CompletedCount"]}, Qualified: {reader["QualifiedCount"]}, Failed: {reader["FailedCount"]}");
    Console.WriteLine($"  Elapsed: {reader["ElapsedMs"]}ms");
    Console.WriteLine($"  Error: {(reader["ErrorMessage"] is DBNull ? "none" : reader["ErrorMessage"])}");
    Console.WriteLine();
}
reader.Close();

// Check result count per run
Console.WriteLine("=== Results per Run ===");
var cmd2 = conn.CreateCommand();
cmd2.CommandText = "SELECT r.Id, r.Symbol, r.Status, COUNT(res.Id) as ResultCount FROM OptimizationRuns r LEFT JOIN OptimizationResults res ON r.Id = res.OptimizationRunId GROUP BY r.Id ORDER BY r.CreatedAtUtc DESC LIMIT 5";
using var r2 = cmd2.ExecuteReader();
while (r2.Read())
{
    Console.WriteLine($"Run {r2["Id"]} ({r2["Symbol"]}, {r2["Status"]}): {r2["ResultCount"]} results");
}
r2.Close();

// Compare configs of all recent runs
Console.WriteLine("\n=== All Recent Runs Thresholds ===");
var cmd3 = conn.CreateCommand();
cmd3.CommandText = "SELECT Id, ThresholdsJson, QualifiedCount, TotalCombinations, StartDateUtc, EndDateUtc, SweepConfigJson FROM OptimizationRuns ORDER BY CreatedAtUtc DESC LIMIT 6";
using var r3 = cmd3.ExecuteReader();
while (r3.Read())
{
    var sweepJson = r3["SweepConfigJson"]?.ToString() ?? "";
    // Extract directions from the JSON
    var dirStart = sweepJson.IndexOf("\"Directions\":");
    var dirEnd = dirStart >= 0 ? sweepJson.IndexOf("]", dirStart) + 1 : -1;
    var dirPart = dirStart >= 0 && dirEnd > dirStart ? sweepJson[dirStart..dirEnd] : "n/a";
    
    // Extract timeframes  
    var tfStart = sweepJson.IndexOf("\"Timeframes\":");
    var tfEnd = tfStart >= 0 ? sweepJson.IndexOf("]", tfStart) + 1 : -1;
    var tfPart = tfStart >= 0 && tfEnd > tfStart ? sweepJson[tfStart..tfEnd] : "n/a";
    
    Console.WriteLine($"ID: {r3["Id"]}");
    Console.WriteLine($"  {r3["QualifiedCount"]}/{r3["TotalCombinations"]} qualified");
    Console.WriteLine($"  Start: {DateTimeOffset.FromUnixTimeMilliseconds((long)r3["StartDateUtc"]):yyyy-MM-dd} End: {DateTimeOffset.FromUnixTimeMilliseconds((long)r3["EndDateUtc"]):yyyy-MM-dd}");
    Console.WriteLine($"  Thresholds: {r3["ThresholdsJson"]}");
    Console.WriteLine($"  {dirPart}  {tfPart}");
    Console.WriteLine();
}
r3.Close();

// dummy block to keep old bottom code from breaking
if (false) {
    long prevTimestamp = 0;
    var duplicateCount = 0;
    var nonAscendingCount = 0;
    var itemIndex = 0;
    JsonElement item = default;
    JsonElement equityArr = default;
    var ts = item.GetProperty("timestampUtc").GetInt64();
    if (ts == prevTimestamp) duplicateCount++;
    if (ts < prevTimestamp) nonAscendingCount++;
    prevTimestamp = ts;
    itemIndex++;

    Console.WriteLine($"Duplicate timestamps: {duplicateCount}");
    Console.WriteLine($"Non-ascending timestamps: {nonAscendingCount}");

    prevTimestamp = long.MinValue;
    duplicateCount = 0;
    foreach (var i2 in equityArr.EnumerateArray())
    {
        var ts2 = i2.GetProperty("timestampUtc").GetInt64();
        var converted = ts2 / 1000;
        if (converted == prevTimestamp) duplicateCount++;
        prevTimestamp = converted;
    }

    Console.WriteLine($"Duplicate CONVERTED timestamps (after /1000): {duplicateCount}");
}
