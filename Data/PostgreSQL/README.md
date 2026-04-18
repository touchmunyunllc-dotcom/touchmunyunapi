# PostgreSQL Database Setup

This directory contains the PostgreSQL database schema and seed data for the e-commerce application.

## Files

- `schema.sql` - Complete database schema with all tables, indexes, constraints, and triggers
- `seed_data.sql` - Sample seed data (5 users, 5 products, 2 coupons, 4 addresses)

## Quick Setup

### Option 1: Using SQL Files (Recommended)

1. **Create the database:**
   ```bash
   psql -U postgres -c "CREATE DATABASE ECommerceDb;"
   ```

2. **Run the schema:**
   ```bash
   psql -U postgres -d ECommerceDb -f schema.sql
   ```

3. **Run the seed data:**
   ```bash
   psql -U postgres -d ECommerceDb -f seed_data.sql
   ```

### Option 2: Automatic (Application Startup)

The application will automatically:
- Create the database if it doesn't exist
- Create all tables and indexes
- Seed initial data if tables are empty

Just update the connection string in `appsettings.json` and run the application.

## Database Schema

### Tables

1. **users** - User accounts (Admin and Customer roles)
2. **addresses** - User shipping addresses
3. **products** - Product catalog with image arrays
4. **coupons** - Discount codes and promotional offers
5. **orders** - Customer orders
6. **order_items** - Individual items within orders
7. **payments** - Payment transaction records

### Key Features

- ✅ UUID primary keys
- ✅ Proper foreign key constraints
- ✅ Check constraints for data validation
- ✅ Indexes for performance optimization
- ✅ Automatic timestamp triggers
- ✅ PostgreSQL array support for product images
- ✅ Full-text search capability

### Indexes

All tables have appropriate indexes for:
- Foreign keys
- Frequently queried columns (email, code, status, etc.)
- Search operations
- Date-based queries

## Seed Data

### Users (5)
- 1 Admin user: `admin@ecommerce.com` / `Admin@123`
- 4 Customer users with default password: `Password123!`

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

## Verification

After seeding, verify the data:

```sql
SELECT COUNT(*) FROM users;        -- Should return 5
SELECT COUNT(*) FROM products;     -- Should return 5
SELECT COUNT(*) FROM coupons;      -- Should return 2
SELECT COUNT(*) FROM addresses;    -- Should return 4
```

## Connection String Format

```
Host=localhost;Port=5432;Database=ECommerceDb;Username=postgres;Password=your_password;
```

## Notes

- All timestamps use `TIMESTAMP WITH TIME ZONE` for proper timezone handling
- Product images are stored as PostgreSQL TEXT arrays
- Passwords in seed data are hashed with BCrypt
- All monetary values use DECIMAL(18, 2) for precision

