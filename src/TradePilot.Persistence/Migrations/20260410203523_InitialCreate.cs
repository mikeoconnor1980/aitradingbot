using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TradePilot.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BacktestRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Symbol = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IntervalsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    StartDateUtc = table.Column<long>(type: "bigint", nullable: false),
                    EndDateUtc = table.Column<long>(type: "bigint", nullable: false),
                    StrategyConfigJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ExecutionConfigJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    InitialCapital = table.Column<double>(type: "float", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Progress = table.Column<int>(type: "int", nullable: false),
                    TotalCandles = table.Column<int>(type: "int", nullable: false),
                    ErrorMessage = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CandlesReplayed = table.Column<int>(type: "int", nullable: false),
                    ElapsedMs = table.Column<long>(type: "bigint", nullable: false),
                    TotalTrades = table.Column<int>(type: "int", nullable: false),
                    WinningTrades = table.Column<int>(type: "int", nullable: false),
                    LosingTrades = table.Column<int>(type: "int", nullable: false),
                    WinRate = table.Column<double>(type: "float", nullable: false),
                    TotalPnl = table.Column<double>(type: "float", nullable: false),
                    MaxDrawdown = table.Column<double>(type: "float", nullable: false),
                    AverageTradePnl = table.Column<double>(type: "float", nullable: false),
                    AverageHoldTimeMinutes = table.Column<double>(type: "float", nullable: false),
                    HedgesOpened = table.Column<int>(type: "int", nullable: false),
                    TotalFeesPaid = table.Column<double>(type: "float", nullable: false),
                    TradesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EquityTimeSeriesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AuditLogEnabled = table.Column<bool>(type: "bit", nullable: false),
                    CandleLogJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OrderEventLogJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    GridCycleLogJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAtUtc = table.Column<long>(type: "bigint", nullable: false),
                    StrategyId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    StrategyRevisionId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BacktestRuns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Candles",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Source = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Hyperliquid"),
                    Symbol = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Interval = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Timestamp = table.Column<long>(type: "bigint", nullable: false),
                    Open = table.Column<double>(type: "float", nullable: false),
                    High = table.Column<double>(type: "float", nullable: false),
                    Low = table.Column<double>(type: "float", nullable: false),
                    Close = table.Column<double>(type: "float", nullable: false),
                    Volume = table.Column<double>(type: "float", nullable: false),
                    NumTrades = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Candles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FundingRates",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Symbol = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Timestamp = table.Column<long>(type: "bigint", nullable: false),
                    Rate = table.Column<double>(type: "float", nullable: false),
                    MarkPrice = table.Column<double>(type: "float", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FundingRates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GridCycles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GridCycleId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    StrategyName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Symbol = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    AnchorPrice = table.Column<double>(type: "float", nullable: false),
                    TotalLevels = table.Column<int>(type: "int", nullable: false),
                    FilledLevels = table.Column<int>(type: "int", nullable: false),
                    Lifecycle = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ClosedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CloseReason = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    RealisedPnl = table.Column<double>(type: "float", nullable: true),
                    UserId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GridCycles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LiveFills",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Symbol = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Side = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Direction = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Price = table.Column<double>(type: "float", nullable: false),
                    Size = table.Column<double>(type: "float", nullable: false),
                    Fee = table.Column<double>(type: "float", nullable: false),
                    ClosedPnl = table.Column<double>(type: "float", nullable: false),
                    FilledAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LiveFills", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LiveOrders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    GridCycleId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Level = table.Column<int>(type: "int", nullable: false),
                    Symbol = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Side = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    OrderType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Price = table.Column<double>(type: "float", nullable: false),
                    Size = table.Column<double>(type: "float", nullable: false),
                    TradeType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PlacedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FilledAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CancelledAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UserId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LiveOrders", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LlmContextSnapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Symbol = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    MarketSentiment = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    MacroRegime = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    EventRisk = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Confidence = table.Column<double>(type: "float", nullable: false),
                    DerivedRegime = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Summary = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    GeneratedAtUtc = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LlmContextSnapshots", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MacroEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Provider = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ProviderEventId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Country = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ScheduledAtUtc = table.Column<long>(type: "bigint", nullable: false),
                    ReleasedAtUtc = table.Column<long>(type: "bigint", nullable: true),
                    Importance = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Actual = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Forecast = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Previous = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Revised = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DefaultPreBlockMinutes = table.Column<int>(type: "int", nullable: false),
                    DefaultPostBlockMinutes = table.Column<int>(type: "int", nullable: false),
                    BlockStartUtc = table.Column<long>(type: "bigint", nullable: false),
                    BlockEndUtc = table.Column<long>(type: "bigint", nullable: false),
                    SourceUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RawPayloadJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    LastSeenUtc = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedAtUtc = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MacroEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MacroSyncRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Provider = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    StartedAtUtc = table.Column<long>(type: "bigint", nullable: false),
                    CompletedAtUtc = table.Column<long>(type: "bigint", nullable: true),
                    Succeeded = table.Column<bool>(type: "bit", nullable: false),
                    EventsFetched = table.Column<int>(type: "int", nullable: false),
                    EventsInserted = table.Column<int>(type: "int", nullable: false),
                    EventsUpdated = table.Column<int>(type: "int", nullable: false),
                    Error = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MacroSyncRuns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OptimizationRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Symbol = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    StartDateUtc = table.Column<long>(type: "bigint", nullable: false),
                    EndDateUtc = table.Column<long>(type: "bigint", nullable: false),
                    InitialCapital = table.Column<double>(type: "float", nullable: false),
                    SweepConfigJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ThresholdsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TotalCombinations = table.Column<int>(type: "int", nullable: false),
                    CompletedCount = table.Column<int>(type: "int", nullable: false),
                    QualifiedCount = table.Column<int>(type: "int", nullable: false),
                    FailedCount = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ErrorMessage = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ElapsedMs = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OptimizationRuns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Strategies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    StrategyType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ConfigJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsRunning = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedAtUtc = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Strategies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAtUtc = table.Column<long>(type: "bigint", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OptimizationResults",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OptimizationRunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Rank = table.Column<int>(type: "int", nullable: false),
                    FitnessScore = table.Column<double>(type: "float", nullable: false),
                    StrategyConfigJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SignalDescription = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TotalPnl = table.Column<double>(type: "float", nullable: false),
                    WinRate = table.Column<double>(type: "float", nullable: false),
                    MaxDrawdown = table.Column<double>(type: "float", nullable: false),
                    TotalTrades = table.Column<int>(type: "int", nullable: false),
                    WinningTrades = table.Column<int>(type: "int", nullable: false),
                    LosingTrades = table.Column<int>(type: "int", nullable: false),
                    TotalFeesPaid = table.Column<double>(type: "float", nullable: false),
                    AverageTradePnl = table.Column<double>(type: "float", nullable: false),
                    AverageHoldTimeMinutes = table.Column<double>(type: "float", nullable: false),
                    OosTotalPnl = table.Column<double>(type: "float", nullable: true),
                    OosWinRate = table.Column<double>(type: "float", nullable: true),
                    OosMaxDrawdown = table.Column<double>(type: "float", nullable: true),
                    OosTotalTrades = table.Column<int>(type: "int", nullable: true),
                    OosFitnessScore = table.Column<double>(type: "float", nullable: true),
                    SharpeRatio = table.Column<double>(type: "float", nullable: true),
                    SortinoRatio = table.Column<double>(type: "float", nullable: true),
                    ProfitFactor = table.Column<double>(type: "float", nullable: true),
                    CalmarRatio = table.Column<double>(type: "float", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OptimizationResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OptimizationResults_OptimizationRuns_OptimizationRunId",
                        column: x => x.OptimizationRunId,
                        principalTable: "OptimizationRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StrategyReviews",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StrategyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RevisionNumber = table.Column<int>(type: "int", nullable: false),
                    ReviewMarkdown = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ModelName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsFallback = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    CreatedAtUtc = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StrategyReviews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StrategyReviews_Strategies_StrategyId",
                        column: x => x.StrategyId,
                        principalTable: "Strategies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StrategyRevisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StrategyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RevisionNumber = table.Column<int>(type: "int", nullable: false),
                    ConfigJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Source = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Label = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ChangeSummary = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    CreatedAtUtc = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StrategyRevisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StrategyRevisions_Strategies_StrategyId",
                        column: x => x.StrategyId,
                        principalTable: "Strategies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserWalletAddresses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Exchange = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    WalletAddress = table.Column<string>(type: "nvarchar(42)", maxLength: 42, nullable: false),
                    CreatedAtUtc = table.Column<long>(type: "bigint", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserWalletAddresses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserWalletAddresses_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BacktestRuns_StrategyId",
                table: "BacktestRuns",
                column: "StrategyId");

            migrationBuilder.CreateIndex(
                name: "IX_Candles_Source_Symbol_Interval_Timestamp",
                table: "Candles",
                columns: new[] { "Source", "Symbol", "Interval", "Timestamp" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FundingRates_Symbol_Timestamp",
                table: "FundingRates",
                columns: new[] { "Symbol", "Timestamp" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GridCycles_GridCycleId",
                table: "GridCycles",
                column: "GridCycleId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GridCycles_Strategy_Symbol_Lifecycle",
                table: "GridCycles",
                columns: new[] { "StrategyName", "Symbol", "Lifecycle" });

            migrationBuilder.CreateIndex(
                name: "IX_GridCycles_UserId",
                table: "GridCycles",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_LiveFills_OrderId",
                table: "LiveFills",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_LiveFills_Symbol_FilledAtUtc",
                table: "LiveFills",
                columns: new[] { "Symbol", "FilledAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_LiveFills_UserId",
                table: "LiveFills",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_LiveOrders_GridCycleId",
                table: "LiveOrders",
                column: "GridCycleId");

            migrationBuilder.CreateIndex(
                name: "IX_LiveOrders_OrderId",
                table: "LiveOrders",
                column: "OrderId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LiveOrders_UserId",
                table: "LiveOrders",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_LlmContextSnapshots_GeneratedAtUtc",
                table: "LlmContextSnapshots",
                column: "GeneratedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_LlmContextSnapshots_Symbol_GeneratedAtUtc",
                table: "LlmContextSnapshots",
                columns: new[] { "Symbol", "GeneratedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_MacroEvents_BlockEndUtc",
                table: "MacroEvents",
                column: "BlockEndUtc");

            migrationBuilder.CreateIndex(
                name: "IX_MacroEvents_BlockStartUtc",
                table: "MacroEvents",
                column: "BlockStartUtc");

            migrationBuilder.CreateIndex(
                name: "IX_MacroEvents_Importance",
                table: "MacroEvents",
                column: "Importance");

            migrationBuilder.CreateIndex(
                name: "IX_MacroEvents_Provider_ProviderEventId",
                table: "MacroEvents",
                columns: new[] { "Provider", "ProviderEventId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MacroEvents_ScheduledAtUtc",
                table: "MacroEvents",
                column: "ScheduledAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_MacroSyncRuns_StartedAtUtc",
                table: "MacroSyncRuns",
                column: "StartedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_OptimizationResults_RunId",
                table: "OptimizationResults",
                column: "OptimizationRunId");

            migrationBuilder.CreateIndex(
                name: "IX_OptimizationResults_RunId_Rank",
                table: "OptimizationResults",
                columns: new[] { "OptimizationRunId", "Rank" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OptimizationRuns_CreatedAtUtc",
                table: "OptimizationRuns",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_Strategies_UserId_IsActive",
                table: "Strategies",
                columns: new[] { "UserId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_Strategies_UserId_Name",
                table: "Strategies",
                columns: new[] { "UserId", "Name" },
                unique: true,
                filter: "[IsActive] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_StrategyReviews_StrategyId_RevisionNumber",
                table: "StrategyReviews",
                columns: new[] { "StrategyId", "RevisionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StrategyRevisions_StrategyId_RevisionNumber",
                table: "StrategyRevisions",
                columns: new[] { "StrategyId", "RevisionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserWalletAddresses_UserId_IsActive",
                table: "UserWalletAddresses",
                columns: new[] { "UserId", "IsActive" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BacktestRuns");

            migrationBuilder.DropTable(
                name: "Candles");

            migrationBuilder.DropTable(
                name: "FundingRates");

            migrationBuilder.DropTable(
                name: "GridCycles");

            migrationBuilder.DropTable(
                name: "LiveFills");

            migrationBuilder.DropTable(
                name: "LiveOrders");

            migrationBuilder.DropTable(
                name: "LlmContextSnapshots");

            migrationBuilder.DropTable(
                name: "MacroEvents");

            migrationBuilder.DropTable(
                name: "MacroSyncRuns");

            migrationBuilder.DropTable(
                name: "OptimizationResults");

            migrationBuilder.DropTable(
                name: "StrategyReviews");

            migrationBuilder.DropTable(
                name: "StrategyRevisions");

            migrationBuilder.DropTable(
                name: "UserWalletAddresses");

            migrationBuilder.DropTable(
                name: "OptimizationRuns");

            migrationBuilder.DropTable(
                name: "Strategies");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
