FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_ENVIRONMENT=Production

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy solution and project files for layer caching
COPY Scaffold.sln .
COPY src/Scaffold.Api/Scaffold.Api.csproj src/Scaffold.Api/
COPY src/Scaffold.Core/Scaffold.Core.csproj src/Scaffold.Core/
COPY src/Scaffold.Assessment/Scaffold.Assessment.csproj src/Scaffold.Assessment/
COPY src/Scaffold.Migration/Scaffold.Migration.csproj src/Scaffold.Migration/
COPY src/Scaffold.Infrastructure/Scaffold.Infrastructure.csproj src/Scaffold.Infrastructure/
COPY tests/Scaffold.Api.Tests/Scaffold.Api.Tests.csproj tests/Scaffold.Api.Tests/
COPY tests/Scaffold.Assessment.Tests/Scaffold.Assessment.Tests.csproj tests/Scaffold.Assessment.Tests/
COPY tests/Scaffold.Migration.Tests/Scaffold.Migration.Tests.csproj tests/Scaffold.Migration.Tests/
COPY tests/Scaffold.Integration.Tests/Scaffold.Integration.Tests.csproj tests/Scaffold.Integration.Tests/

# Restore dependencies
RUN dotnet restore Scaffold.sln

# Copy remaining source
COPY src/ src/

# Build and publish
RUN dotnet publish src/Scaffold.Api/Scaffold.Api.csproj -c Release -o /app/publish --no-restore

FROM base AS final
WORKDIR /app

# Create non-root user
RUN adduser --disabled-password --gecos "" --uid 1000 appuser \
    && chown -R appuser:appuser /app

COPY --from=build --chown=appuser:appuser /app/publish .

USER appuser
ENTRYPOINT ["dotnet", "Scaffold.Api.dll"]
