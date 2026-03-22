# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copiar e restaurar dependências primeiro (cache otimizado)
COPY ["GlobalLogistics.AuthAPI/GlobalLogistics.AuthAPI.csproj", "GlobalLogistics.AuthAPI/"]
RUN dotnet restore "GlobalLogistics.AuthAPI/GlobalLogistics.AuthAPI.csproj"

# Copiar restante do código e buildar
COPY . .
WORKDIR "/src/GlobalLogistics.AuthAPI"
RUN dotnet publish -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app/publish .

# Instalar curl para healthcheck
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

# Expor porta e configurar entrypoint
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "GlobalLogistics.AuthAPI.dll"]
