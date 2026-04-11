# ---------- restore ----------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS restore
WORKDIR /src

# Copy solution + project files first (better layer caching)
COPY TradingApp.sln Directory.Build.props Directory.Build.targets nuget.config ./
COPY src/TradingApp.Api/TradingApp.Api.csproj                   src/TradingApp.Api/
COPY src/TradingApp.Application/TradingApp.Application.csproj   src/TradingApp.Application/
COPY src/TradingApp.Domain/TradingApp.Domain.csproj             src/TradingApp.Domain/
COPY src/TradingApp.Infrastructure/TradingApp.Infrastructure.csproj src/TradingApp.Infrastructure/
COPY src/TradingApp.Persistence/TradingApp.Persistence.csproj   src/TradingApp.Persistence/
COPY src/TradingApp.AI/TradingApp.AI.csproj                     src/TradingApp.AI/
COPY src/TradingApp.Indicators/TradingApp.Indicators.csproj     src/TradingApp.Indicators/
COPY src/TradingApp.Worker/TradingApp.Worker.csproj             src/TradingApp.Worker/
COPY tests/TradingApp.Domain.Tests/TradingApp.Domain.Tests.csproj           tests/TradingApp.Domain.Tests/
COPY tests/TradingApp.Application.Tests/TradingApp.Application.Tests.csproj tests/TradingApp.Application.Tests/
COPY tests/TradingApp.AI.Tests/TradingApp.AI.Tests.csproj                   tests/TradingApp.AI.Tests/
COPY tests/TradingApp.Infrastructure.Tests/TradingApp.Infrastructure.Tests.csproj tests/TradingApp.Infrastructure.Tests/
COPY tests/TradingApp.Persistence.Tests/TradingApp.Persistence.Tests.csproj tests/TradingApp.Persistence.Tests/
COPY tests/TradingApp.Api.Tests/TradingApp.Api.Tests.csproj                 tests/TradingApp.Api.Tests/
COPY tests/TradingApp.Indicators.Tests/TradingApp.Indicators.Tests.csproj   tests/TradingApp.Indicators.Tests/
COPY tests/TradingApp.Worker.Tests/TradingApp.Worker.Tests.csproj           tests/TradingApp.Worker.Tests/

RUN dotnet restore TradingApp.sln

# ---------- publish ----------
FROM restore AS publish
COPY src/ src/
COPY tests/ tests/
RUN dotnet publish src/TradingApp.Api/TradingApp.Api.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

# ---------- runtime ----------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

USER $APP_UID

COPY --from=publish /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "TradingApp.Api.dll"]
