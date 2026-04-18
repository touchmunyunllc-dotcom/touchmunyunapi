# Database Setup Script for TouchMunyun
# This script will help you set up the database after changing the connection string

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "TouchMunyun Database Setup" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Check if .NET is installed
Write-Host "Checking .NET installation..." -ForegroundColor Yellow
$dotnetVersion = dotnet --version
if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: .NET SDK is not installed or not in PATH" -ForegroundColor Red
    exit 1
}
Write-Host "✓ .NET SDK $dotnetVersion found" -ForegroundColor Green
Write-Host ""

# Check connection string
Write-Host "Checking connection string configuration..." -ForegroundColor Yellow
$appsettingsPath = "appsettings.json"
if (Test-Path $appsettingsPath) {
    $appsettings = Get-Content $appsettingsPath | ConvertFrom-Json
    $connectionString = $appsettings.ConnectionStrings.DefaultConnection
    if ($connectionString) {
        Write-Host "✓ Connection string found in appsettings.json" -ForegroundColor Green
        Write-Host "  Database: touchmunyun" -ForegroundColor Gray
    } else {
        Write-Host "⚠ Connection string not found in appsettings.json" -ForegroundColor Yellow
    }
} else {
    Write-Host "⚠ appsettings.json not found" -ForegroundColor Yellow
}
Write-Host ""

# Option 1: Automatic Setup (Recommended)
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Option 1: Automatic Setup (Recommended)" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "The backend application will automatically:" -ForegroundColor White
Write-Host "  1. Create the database if it doesn't exist" -ForegroundColor Gray
Write-Host "  2. Create all tables and indexes" -ForegroundColor Gray
Write-Host "  3. Run migrations" -ForegroundColor Gray
Write-Host "  4. Seed initial data" -ForegroundColor Gray
Write-Host ""
Write-Host "To run automatic setup, execute:" -ForegroundColor Yellow
Write-Host "  dotnet run" -ForegroundColor White
Write-Host ""

# Option 2: Manual SQL Script Execution
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Option 2: Manual SQL Script Execution" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "If you prefer to run SQL scripts manually:" -ForegroundColor White
Write-Host ""
Write-Host "1. Connect to your Neon database:" -ForegroundColor Yellow
Write-Host '   psql "Host=ep-aged-cake-a1s32hg2-pooler.ap-southeast-1.aws.neon.tech;Database=touchmunyun;Username=neondb_owner;Password=npg_u8W4FEagdnoK;Ssl Mode=Require;Trust Server Certificate=true;"' -ForegroundColor Gray
Write-Host ""
Write-Host "2. Run the schema script:" -ForegroundColor Yellow
Write-Host '   psql "Host=ep-aged-cake-a1s32hg2-pooler.ap-southeast-1.aws.neon.tech;Database=touchmunyun;Username=neondb_owner;Password=npg_u8W4FEagdnoK;Ssl Mode=Require;Trust Server Certificate=true;" -f Data\PostgreSQL\schema.sql' -ForegroundColor Gray
Write-Host ""
Write-Host "3. Run the seed data script:" -ForegroundColor Yellow
Write-Host '   psql "Host=ep-aged-cake-a1s32hg2-pooler.ap-southeast-1.aws.neon.tech;Database=touchmunyun;Username=neondb_owner;Password=npg_u8W4FEagdnoK;Ssl Mode=Require;Trust Server Certificate=true;" -f Data\PostgreSQL\seed_data.sql' -ForegroundColor Gray
Write-Host ""

# Ask user what they want to do
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "What would you like to do?" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "1. Run automatic setup (dotnet run)" -ForegroundColor Green
Write-Host "2. Show connection string details" -ForegroundColor Yellow
Write-Host "3. Exit" -ForegroundColor Red
Write-Host ""

$choice = Read-Host "Enter your choice (1-3)"

switch ($choice) {
    "1" {
        Write-Host ""
        Write-Host "Starting backend application..." -ForegroundColor Green
        Write-Host "The database will be initialized automatically on startup." -ForegroundColor Yellow
        Write-Host ""
        dotnet run
    }
    "2" {
        Write-Host ""
        Write-Host "Connection String Details:" -ForegroundColor Cyan
        Write-Host "=========================" -ForegroundColor Cyan
        Write-Host "Host: ep-aged-cake-a1s32hg2-pooler.ap-southeast-1.aws.neon.tech" -ForegroundColor White
        Write-Host "Database: touchmunyun" -ForegroundColor White
        Write-Host "Username: neondb_owner" -ForegroundColor White
        Write-Host "SSL Mode: Require" -ForegroundColor White
        Write-Host ""
        Write-Host "Full Connection String:" -ForegroundColor Yellow
        Write-Host "Host=ep-aged-cake-a1s32hg2-pooler.ap-southeast-1.aws.neon.tech;Database=touchmunyun;Username=neondb_owner;Password=npg_u8W4FEagdnoK;Ssl Mode=Require;Trust Server Certificate=true;" -ForegroundColor Gray
        Write-Host ""
    }
    "3" {
        Write-Host "Exiting..." -ForegroundColor Yellow
        exit 0
    }
    default {
        Write-Host "Invalid choice. Exiting..." -ForegroundColor Red
        exit 1
    }
}

