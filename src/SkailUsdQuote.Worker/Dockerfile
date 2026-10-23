# Build context: repository root.
#   podman build -f src/SkailUsdQuote.Worker/Dockerfile -t usd-quote-worker .
# The IL weaving of Skail.Platform.Runtime.Build happens in dotnet publish (SDK image);
# the final image only needs the .NET runtime.
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY nuget.config ./
COPY src/SkailUsdQuote.Contracts/SkailUsdQuote.Contracts.csproj src/SkailUsdQuote.Contracts/
COPY src/SkailUsdQuote.Worker/SkailUsdQuote.Worker.csproj src/SkailUsdQuote.Worker/
RUN dotnet restore src/SkailUsdQuote.Worker/SkailUsdQuote.Worker.csproj
COPY src/SkailUsdQuote.Contracts src/SkailUsdQuote.Contracts
COPY src/SkailUsdQuote.Worker src/SkailUsdQuote.Worker
RUN dotnet publish src/SkailUsdQuote.Worker/SkailUsdQuote.Worker.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/runtime:8.0
WORKDIR /app
COPY --from=build /app .
ENTRYPOINT ["dotnet", "SkailUsdQuote.Worker.dll"]
