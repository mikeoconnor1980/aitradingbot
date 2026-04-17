# ---------- restore ----------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS restore
WORKDIR /src

# Copy solution + project files first (better layer caching)
COPY TradePilot.sln Directory.Build.props Directory.Build.targets nuget.config ./
COPY src/TradePilot.Api/TradePilot.Api.csproj                   src/TradePilot.Api/
COPY src/TradePilot.Application/TradePilot.Application.csproj   src/TradePilot.Application/
COPY src/TradePilot.Domain/TradePilot.Domain.csproj             src/TradePilot.Domain/
COPY src/TradePilot.Infrastructure/TradePilot.Infrastructure.csproj src/TradePilot.Infrastructure/
COPY src/TradePilot.Persistence/TradePilot.Persistence.csproj   src/TradePilot.Persistence/
COPY src/TradePilot.AI/TradePilot.AI.csproj                     src/TradePilot.AI/
COPY src/TradePilot.Indicators/TradePilot.Indicators.csproj     src/TradePilot.Indicators/
COPY src/TradePilot.Worker/TradePilot.Worker.csproj             src/TradePilot.Worker/
COPY tests/TradePilot.Domain.Tests/TradePilot.Domain.Tests.csproj           tests/TradePilot.Domain.Tests/
COPY tests/TradePilot.Application.Tests/TradePilot.Application.Tests.csproj tests/TradePilot.Application.Tests/
COPY tests/TradePilot.AI.Tests/TradePilot.AI.Tests.csproj                   tests/TradePilot.AI.Tests/
COPY tests/TradePilot.Infrastructure.Tests/TradePilot.Infrastructure.Tests.csproj tests/TradePilot.Infrastructure.Tests/
COPY tests/TradePilot.Persistence.Tests/TradePilot.Persistence.Tests.csproj tests/TradePilot.Persistence.Tests/
COPY tests/TradePilot.Api.Tests/TradePilot.Api.Tests.csproj                 tests/TradePilot.Api.Tests/
COPY tests/TradePilot.Indicators.Tests/TradePilot.Indicators.Tests.csproj   tests/TradePilot.Indicators.Tests/
COPY tests/TradePilot.Worker.Tests/TradePilot.Worker.Tests.csproj           tests/TradePilot.Worker.Tests/

RUN dotnet restore TradePilot.sln

# ---------- publish ----------
FROM restore AS publish
COPY src/ src/
COPY tests/ tests/
RUN dotnet publish src/TradePilot.Api/TradePilot.Api.csproj \
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

ENTRYPOINT ["dotnet", "TradePilot.Api.dll"]
