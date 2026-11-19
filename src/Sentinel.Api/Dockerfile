FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy solution-level build configuration and project files for layer caching
COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/Sentinel.Domain/Sentinel.Domain.csproj src/Sentinel.Domain/
COPY src/Sentinel.Application/Sentinel.Application.csproj src/Sentinel.Application/
COPY src/Sentinel.Infrastructure/Sentinel.Infrastructure.csproj src/Sentinel.Infrastructure/
COPY src/Sentinel.ServiceDefaults/Sentinel.ServiceDefaults.csproj src/Sentinel.ServiceDefaults/
COPY src/Sentinel.Api/Sentinel.Api.csproj src/Sentinel.Api/

RUN dotnet restore src/Sentinel.Api/Sentinel.Api.csproj

# Copy source and publish
COPY src/Sentinel.Domain/ src/Sentinel.Domain/
COPY src/Sentinel.Application/ src/Sentinel.Application/
COPY src/Sentinel.Infrastructure/ src/Sentinel.Infrastructure/
COPY src/Sentinel.ServiceDefaults/ src/Sentinel.ServiceDefaults/
COPY src/Sentinel.Api/ src/Sentinel.Api/

RUN dotnet publish src/Sentinel.Api/Sentinel.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "Sentinel.Api.dll"]
