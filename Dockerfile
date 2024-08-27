#FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build-env
FROM mcr.microsoft.com/dotnet/core/aspnet:3.1-buster-slim AS base
FROM mcr.microsoft.com/dotnet/core/sdk:3.1-buster AS build-env
WORKDIR /app

# Copia csproj e ripristina le dipendenze
COPY TelegramBot/TelegramBot.csproj TelegramBot/
COPY StatsBot/StatsBot.csproj StatsBot/
COPY Scraper/Scraper.csproj Scraper/
COPY Model/Model.csproj Model/
COPY BetBot/BetBot.csproj BetBot/

RUN dotnet restore ./TelegramBot/TelegramBot.csproj

# Copia tutto e buildalo
COPY . ./
RUN dotnet publish ./TelegramBot -c Release -o out

# Genera l'immagine di runtime
FROM mcr.microsoft.com/dotnet/core/aspnet:3.1-buster-slim
WORKDIR /app
COPY --from=build-env /app/out .
ENTRYPOINT ["dotnet", "TelegramBot.dll"]