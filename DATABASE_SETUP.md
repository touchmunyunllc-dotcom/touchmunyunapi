# Database Setup Guide

This guide will help you set up the database after changing the connection string.

## Connection String

Your current connection string points to:
- **Database**: `touchmunyun`
- **Host**: `ep-aged-cake-a1s32hg2-pooler.ap-southeast-1.aws.neon.tech`
- **Provider**: Neon PostgreSQL (Cloud)

## Setup Options

### Option 1: Automatic Setup (Recommended)

The backend application will automatically:
1. Create the database if it doesn't exist
2. Create all tables and indexes
3. Run migrations
4. Seed initial data

**Steps:**
1. Ensure your connection string is correct in `appsettings.json` or `appsettings.Development.json`
2. Run the backend application:
   ```bash
   cd backend
   dotnet run
   ```
3. The application will automatically initialize the database on startup

### Option 2: Manual SQL Script Execution

If you prefer to run the scripts manually:

#### Prerequisites
- PostgreSQL client (`psql`) installed
- Access to your Neon database

#### Steps

1. **Connect to your database:**
   ```bash
   psql "Host=ep-aged-cake-a1s32hg2-pooler.ap-southeast-1.aws.neon.tech;Database=touchmunyun;Username=neondb_owner;Password=npg_u8W4FEagdnoK;Ssl Mode=Require;Trust Server Certificate=true;"
   ```

2. **Run the schema script:**
   ```bash
   psql "Host=ep-aged-cake-a1s32hg2-pooler.ap-southeast-1.aws.neon.tech;Database=touchmunyun;Username=neondb_owner;Password=npg_u8W4FEagdnoK;Ssl Mode=Require;Trust Server Certificate=true;" -f Data/PostgreSQL/schema.sql
   ```

3. **Run the seed data script:**
   ```bash
   psql "Host=ep-aged-cake-a1s32hg2-pooler.ap-southeast-1.aws.neon.tech;Database=touchmunyun;Username=neondb_owner;Password=npg_u8W4FEagdnoK;Ssl Mode=Require;Trust Server Certificate=true;" -f Data/PostgreSQL/seed_data.sql
   ```

4. **Run migrations (if needed):**
   ```bash
   psql "Host=ep-aged-cake-a1s32hg2-pooler.ap-southeast-1.aws.neon.tech;Database=touchmunyun;Username=neondb_owner;Password=npg_u8W4FEagdnoK;Ssl Mode=Require;Trust Server Certificate=true;" -f Data/PostgreSQL/migration_add_cancellation_fields.sql
   ```

### Option 3: Using .NET CLI Tool

You can also create a simple console command to run the scripts:

```bash
cd backend
dotnet run -- --setup-database
```

## Verification

After setup, verify the database:

```sql
-- Check table counts
SELECT COUNT(*) FROM users;        -- Should return 5 (if seeded)
SELECT COUNT(*) FROM products;     -- Should return 5 (if seeded)
SELECT COUNT(*) FROM coupons;      -- Should return 2 (if seeded)
SELECT COUNT(*) FROM addresses;    -- Should return 4 (if seeded)

-- Check if tables exist
SELECT table_name 
FROM information_schema.tables 
WHERE table_schema = 'public' 
ORDER BY table_name;
```

## Seed Data

The seed data includes:

### Users (5)
- **Admin**: `admin@ecommerce.com` / `Admin@123`
- **Customers**: 
  - `john.doe@example.com` / `Password123!`
  - `jane.smith@example.com` / `Password123!`
  - `bob.johnson@example.com` / `Password123!`
  - `alice.williams@example.com` / `Password123!`

### Products (5)
- Wireless Bluetooth Headphones - $129.99
- Smart Fitness Watch - $249.99
- Organic Cotton T-Shirt - $29.99
- Stainless Steel Water Bottle - $34.99
- Premium Coffee Beans - $24.99

### Coupons (2)
- `WELCOME10` - 10% off (min $50, max $25 discount)
- `SAVE20` - 20% off (min $100, max $50 discount)

### Addresses (4)
- One default address for each customer user

## Troubleshooting

### Connection Issues
- Ensure SSL mode is set to `Require` for Neon databases
- Verify the connection string format matches Neon's requirements
- Check if the database name exists in your Neon project

### Migration Errors
- The application handles migrations automatically
- If you see "table already exists" errors, that's normal - the scripts use `CREATE TABLE IF NOT EXISTS`
- Check the application logs for detailed error messages

### Seed Data Not Appearing
- Seed data only runs if tables are empty
- To re-seed, you may need to clear existing data first
- Check the console output for seeding confirmation messages

## Notes

- All passwords are hashed using BCrypt
- The application automatically handles database initialization on startup
- Migrations are idempotent (safe to run multiple times)
- The database uses UUID primary keys
- All timestamps use `TIMESTAMP WITH TIME ZONE`

