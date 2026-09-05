# Build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY HealthGrid.Api/HealthGrid.Api.csproj HealthGrid.Api/
RUN dotnet restore HealthGrid.Api/HealthGrid.Api.csproj
COPY . .
RUN dotnet publish HealthGrid.Api/HealthGrid.Api.csproj -c Release -o /app --no-restore

# Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "HealthGrid.Api.dll"]
