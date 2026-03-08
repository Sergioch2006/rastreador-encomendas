FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["GlobalLogistics.Domain/GlobalLogistics.Domain.csproj", "GlobalLogistics.Domain/"]
COPY ["GlobalLogistics.Application/GlobalLogistics.Application.csproj", "GlobalLogistics.Application/"]
COPY ["GlobalLogistics.Infrastructure/GlobalLogistics.Infrastructure.csproj", "GlobalLogistics.Infrastructure/"]
COPY ["GlobalLogistics.Worker/GlobalLogistics.Worker.csproj", "GlobalLogistics.Worker/"]
RUN dotnet restore "GlobalLogistics.Worker/GlobalLogistics.Worker.csproj"
COPY . .
RUN dotnet build "GlobalLogistics.Worker/GlobalLogistics.Worker.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "GlobalLogistics.Worker/GlobalLogistics.Worker.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "GlobalLogistics.Worker.dll"]
