using Microsoft.Data.Sqlite;

using var conn = new SqliteConnection("Data Source=../src/TradingApp.Api/Data/tradingapp.db");
conn.Open();

Console.WriteLine("=== Users ===");
var cmd = conn.CreateCommand();
cmd.CommandText = "SELECT Id, TelegramChatId FROM Users";
using var reader = cmd.ExecuteReader();
while (reader.Read())
{
    Console.WriteLine($"User={reader[0]}, TgChat={(reader.IsDBNull(1) ? "NULL" : reader[1])}");
}
reader.Close();

Console.WriteLine("\n=== UserWalletAddresses ===");
var cmd2 = conn.CreateCommand();
cmd2.CommandText = "SELECT UserId, WalletAddress, IsActive FROM UserWalletAddresses";
using var reader2 = cmd2.ExecuteReader();
while (reader2.Read())
{
    Console.WriteLine($"User={reader2[0]}, Wallet={reader2[1]}, Active={(reader2.IsDBNull(2) ? "NULL" : reader2.GetBoolean(2))}");
}
