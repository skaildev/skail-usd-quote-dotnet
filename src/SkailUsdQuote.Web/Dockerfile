# Build context: repository root.
#   podman build -f src/SkailUsdQuote.Web/Dockerfile -t usd-quote-web .
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY nuget.config ./
COPY src/SkailUsdQuote.Contracts/SkailUsdQuote.Contracts.csproj src/SkailUsdQuote.Contracts/
COPY src/SkailUsdQuote.Web/SkailUsdQuote.Web.csproj src/SkailUsdQuote.Web/
RUN dotnet restore src/SkailUsdQuote.Web/SkailUsdQuote.Web.csproj
COPY src/SkailUsdQuote.Contracts src/SkailUsdQuote.Contracts
COPY src/SkailUsdQuote.Web src/SkailUsdQuote.Web
RUN dotnet publish src/SkailUsdQuote.Web/SkailUsdQuote.Web.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "SkailUsdQuote.Web.dll"]
