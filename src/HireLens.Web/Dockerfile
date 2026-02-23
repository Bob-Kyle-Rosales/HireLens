# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["HireLens.sln", "."]
COPY ["src/HireLens.Web/HireLens.Web.csproj", "src/HireLens.Web/"]
COPY ["src/HireLens.Application/HireLens.Application.csproj", "src/HireLens.Application/"]
COPY ["src/HireLens.Infrastructure/HireLens.Infrastructure.csproj", "src/HireLens.Infrastructure/"]
COPY ["src/HireLens.Domain/HireLens.Domain.csproj", "src/HireLens.Domain/"]

RUN dotnet restore "src/HireLens.Web/HireLens.Web.csproj"

COPY . .
RUN dotnet publish "src/HireLens.Web/HireLens.Web.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "HireLens.Web.dll"]
