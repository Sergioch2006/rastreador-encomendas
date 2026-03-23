FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["GlobalLogistics.Gateway/GlobalLogistics.Gateway.csproj", "GlobalLogistics.Gateway/"]
RUN dotnet restore "GlobalLogistics.Gateway/GlobalLogistics.Gateway.csproj"
COPY . .
RUN dotnet build "GlobalLogistics.Gateway/GlobalLogistics.Gateway.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "GlobalLogistics.Gateway/GlobalLogistics.Gateway.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "GlobalLogistics.Gateway.dll"]
