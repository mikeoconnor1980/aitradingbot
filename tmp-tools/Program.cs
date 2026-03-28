using Microsoft.Data.Sqlite;
using System.Text.Json;

using var conn = new SqliteConnection("Data Source=../data/tradingapp.db");
conn.Open();

// 1. Distinct symbols and intervals
var cmd = conn.CreateCommand();
cmd.CommandText = "SELECT DISTINCT Symbol, Interval, Source, COUNT(*) as Cnt FROM Candles GROUP BY Symbol, Interval, Source";
using var reader = cmd.ExecuteReader();
Console.WriteLine("Symbol | Interval | Source | Count");
Console.WriteLine("-------|----------|--------|------");
while (reader.Read())
{
    Console.WriteLine($"{reader["Symbol"]} | {reader["Interval"]} | {reader["Source"]} | {reader["Cnt"]}");
}
reader.Close();

// 2. Sample BTC candles
Console.WriteLine("\n--- Sample BTC 15m candles (last 5) ---");
var cmd2 = conn.CreateCommand();
cmd2.CommandText = "SELECT Symbol, Interval, Timestamp, Open, Close, Source FROM Candles WHERE Symbol LIKE '%BTC%' AND Interval = '15m' ORDER BY Timestamp DESC LIMIT 5";
using var r2 = cmd2.ExecuteReader();
while (r2.Read())
{
    Console.WriteLine($"{r2["Symbol"]} | {r2["Interval"]} | ts={r2["Timestamp"]} | o={r2["Open"]} c={r2["Close"]} | src={r2["Source"]}");
}
r2.Close();

// 3. Min/max timestamps for BTC
Console.WriteLine("\n--- BTC timestamp range ---");
var cmd3 = conn.CreateCommand();
cmd3.CommandText = "SELECT Symbol, MIN(Timestamp) as MinTs, MAX(Timestamp) as MaxTs FROM Candles WHERE Symbol LIKE '%BTC%' GROUP BY Symbol";
using var r3 = cmd3.ExecuteReader();
while (r3.Read())
{
    var minTs = (long)r3["MinTs"];
    var maxTs = (long)r3["MaxTs"];
    Console.WriteLine($"{r3["Symbol"]} | min={minTs} ({DateTimeOffset.FromUnixTimeMilliseconds(minTs):yyyy-MM-dd}) | max={maxTs} ({DateTimeOffset.FromUnixTimeMilliseconds(maxTs):yyyy-MM-dd})");
}

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
