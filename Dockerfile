# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj and restore dependencies
COPY ["ECommerce.Api.csproj", "./"]
RUN dotnet restore "ECommerce.Api.csproj"

# Copy everything else and publish
COPY . .
WORKDIR "/src"
RUN dotnet publish "ECommerce.Api.csproj" -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
EXPOSE 80

# Create non-root user
RUN adduser --disabled-password --gecos "" appuser

# aspnet runtime image does not include curl; install for HEALTHCHECK only
USER root
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

USER appuser

HEALTHCHECK --interval=30s --timeout=3s --start-period=10s --retries=3 \
  CMD curl -fsS http://localhost:80/api/version/health || exit 1

ENTRYPOINT ["dotnet", "ECommerce.Api.dll"]

