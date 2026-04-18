using System.Data;
using Npgsql;
using Dapper;
using ECommerce.Models;
using Serilog;

namespace ECommerce.Data;

public interface IDbContext
{
    IDbConnection Connection { get; }
    Task InitializeDatabaseAsync();
}

public class DbContext : IDbContext
{
    private readonly string _connectionString;

    public DbContext(IDbConnection connection)
    {
        Connection = connection;
        _connectionString = connection.ConnectionString ?? string.Empty;
    }

    public IDbConnection Connection { get; private set; }

    public async Task InitializeDatabaseAsync()
    {
        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        // Create database if it doesn't exist
        var builder = new NpgsqlConnectionStringBuilder(_connectionString);
        var dbName = builder.Database;
        builder.Database = "postgres"; // Connect to default database

        using var masterConnection = new NpgsqlConnection(builder.ConnectionString);
        await masterConnection.OpenAsync();

        // Check if database exists
        var dbExists = await masterConnection.QueryFirstOrDefaultAsync<int>(
            "SELECT 1 FROM pg_database WHERE datname = @dbName",
            new { dbName });

        if (dbExists == 0)
        {
            await masterConnection.ExecuteAsync($"CREATE DATABASE \"{dbName}\"");
        }

        await masterConnection.CloseAsync();

        // Create tables by executing schema SQL
        await ExecuteSchemaAsync(connection);
        
        // Run migrations to add new columns if they don't exist
        await RunMigrationsAsync(connection);
    }

    private async Task ExecuteSchemaAsync(IDbConnection connection)
    {
        // Read and execute schema SQL file
        var schemaPath = Path.Combine(AppContext.BaseDirectory, "Data", "PostgreSQL", "schema.sql");
        
        if (File.Exists(schemaPath))
        {
            var schemaSql = await File.ReadAllTextAsync(schemaPath);
            // Split by semicolons and execute each statement
            var statements = schemaSql.Split(';', StringSplitOptions.RemoveEmptyEntries)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s.Trim());

            foreach (var statement in statements)
            {
                if (!string.IsNullOrWhiteSpace(statement) && !statement.StartsWith("--"))
                {
                    try
                    {
                        await connection.ExecuteAsync(statement);
                    }
                    catch (Exception ex)
                    {
                        // Log error but continue (table might already exist)
                        Log.Warning(ex, "Schema execution warning: {Message}", ex.Message);
                    }
                }
            }
        }
        else
        {
            // Fallback: Create tables programmatically if schema file doesn't exist
            await CreateTablesAsync(connection);
        }
    }

    private async Task CreateTablesAsync(IDbConnection connection)
    {
        // Enable UUID extension
        await connection.ExecuteAsync("CREATE EXTENSION IF NOT EXISTS \"uuid-ossp\"");

        // Users table
        await connection.ExecuteAsync(@"
            CREATE TABLE IF NOT EXISTS users (
                id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
                name VARCHAR(255) NOT NULL,
                email VARCHAR(255) NOT NULL UNIQUE,
                password VARCHAR(255),
                role VARCHAR(50) NOT NULL DEFAULT 'Customer' CHECK (role IN ('Customer', 'Admin')),
                cancellation_count INTEGER NOT NULL DEFAULT 0,
                order_blocked_until TIMESTAMP WITH TIME ZONE,
                provider VARCHAR(50),
                provider_id VARCHAR(255),
                phone_number VARCHAR(30),
                created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT CURRENT_TIMESTAMP,
                updated_at TIMESTAMP WITH TIME ZONE,
                CONSTRAINT unique_provider_user UNIQUE (provider, provider_id)
            )");

        // Products table
        await connection.ExecuteAsync(@"
            CREATE TABLE IF NOT EXISTS products (
                id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
                name VARCHAR(255) NOT NULL,
                description TEXT,
                price DECIMAL(18, 2) NOT NULL CHECK (price >= 0),
                sale_price DECIMAL(18, 2) CHECK (sale_price >= 0 AND (sale_price IS NULL OR sale_price < price)),
                images TEXT[] DEFAULT ARRAY[]::TEXT[],
                available_quantity INTEGER NOT NULL DEFAULT 0 CHECK (available_quantity >= 0),
                category VARCHAR(100),
                sku VARCHAR(100) UNIQUE,
                is_active BOOLEAN DEFAULT TRUE,
                created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT CURRENT_TIMESTAMP,
                updated_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT CURRENT_TIMESTAMP
            )");

        // Addresses table
        await connection.ExecuteAsync(@"
            CREATE TABLE IF NOT EXISTS addresses (
                id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
                user_id UUID NOT NULL,
                address_line1 VARCHAR(255) NOT NULL,
                address_line2 VARCHAR(255),
                city VARCHAR(100) NOT NULL,
                state VARCHAR(100) NOT NULL,
                postal_code VARCHAR(20) NOT NULL,
                country VARCHAR(100) NOT NULL DEFAULT 'United States',
                phone VARCHAR(30),
                is_default BOOLEAN DEFAULT FALSE,
                created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT CURRENT_TIMESTAMP,
                updated_at TIMESTAMP WITH TIME ZONE,
                CONSTRAINT fk_addresses_user FOREIGN KEY (user_id) REFERENCES users(id) ON DELETE CASCADE
            )");

        // Cart Items table
        await connection.ExecuteAsync(@"
            CREATE TABLE IF NOT EXISTS cart_items (
                id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
                user_id UUID NOT NULL,
                product_id UUID NOT NULL,
                quantity INTEGER NOT NULL CHECK (quantity > 0),
                created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT CURRENT_TIMESTAMP,
                updated_at TIMESTAMP WITH TIME ZONE,
                CONSTRAINT fk_cart_items_user FOREIGN KEY (user_id) REFERENCES users(id) ON DELETE CASCADE,
                CONSTRAINT fk_cart_items_product FOREIGN KEY (product_id) REFERENCES products(id) ON DELETE CASCADE,
                CONSTRAINT uq_cart_user_product UNIQUE (user_id, product_id)
            )");

        // Coupons table
        await connection.ExecuteAsync(@"
            CREATE TABLE IF NOT EXISTS coupons (
                id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
                code VARCHAR(50) NOT NULL UNIQUE,
                discount_type VARCHAR(50) NOT NULL CHECK (discount_type IN ('Percentage', 'FixedAmount')),
                discount_value DECIMAL(18, 2) NOT NULL CHECK (discount_value > 0),
                expiry_date TIMESTAMP WITH TIME ZONE,
                usage_limit INTEGER,
                usage_count INTEGER DEFAULT 0 CHECK (usage_count >= 0),
                is_active BOOLEAN DEFAULT TRUE,
                min_purchase_amount DECIMAL(18, 2) DEFAULT 0 CHECK (min_purchase_amount >= 0),
                max_discount_amount DECIMAL(18, 2),
                created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT CURRENT_TIMESTAMP
            )");

        // Orders table
        await connection.ExecuteAsync(@"
            CREATE TABLE IF NOT EXISTS orders (
                id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
                order_code VARCHAR(20) NOT NULL UNIQUE,
                user_id UUID,
                guest_email VARCHAR(255),
                total_amount DECIMAL(18, 2) NOT NULL CHECK (total_amount >= 0),
                status VARCHAR(50) NOT NULL DEFAULT 'Pending' CHECK (status IN ('Pending', 'Paid', 'Packed', 'Shipped', 'Delivered', 'Cancelled')),
                coupon_id UUID,
                shipping_address_id UUID,
                stripe_payment_intent_id VARCHAR(255),
                tracking_number VARCHAR(100),
                tracking_url VARCHAR(500),
                notes TEXT,
                cancellation_reason TEXT,
                created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT CURRENT_TIMESTAMP,
                updated_at TIMESTAMP WITH TIME ZONE,
                CONSTRAINT fk_orders_user FOREIGN KEY (user_id) REFERENCES users(id) ON DELETE SET NULL,
                CONSTRAINT fk_orders_coupon FOREIGN KEY (coupon_id) REFERENCES coupons(id) ON DELETE SET NULL,
                CONSTRAINT fk_orders_address FOREIGN KEY (shipping_address_id) REFERENCES addresses(id) ON DELETE SET NULL
            )");

        // OrderItems table
        await connection.ExecuteAsync(@"
            CREATE TABLE IF NOT EXISTS order_items (
                id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
                order_id UUID NOT NULL,
                product_id UUID NOT NULL,
                quantity INTEGER NOT NULL CHECK (quantity > 0),
                price DECIMAL(18, 2) NOT NULL CHECK (price >= 0),
                created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT CURRENT_TIMESTAMP,
                CONSTRAINT fk_order_items_order FOREIGN KEY (order_id) REFERENCES orders(id) ON DELETE CASCADE,
                CONSTRAINT fk_order_items_product FOREIGN KEY (product_id) REFERENCES products(id) ON DELETE RESTRICT
            )");

        // Payments table
        await connection.ExecuteAsync(@"
            CREATE TABLE IF NOT EXISTS payments (
                id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
                order_id UUID NOT NULL,
                stripe_payment_id VARCHAR(255) NOT NULL UNIQUE,
                amount DECIMAL(18, 2) NOT NULL CHECK (amount >= 0),
                status VARCHAR(50) NOT NULL CHECK (status IN ('Pending', 'Processing', 'Completed', 'Failed', 'Refunded')),
                payment_method VARCHAR(50),
                currency VARCHAR(10) DEFAULT 'USD',
                created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT CURRENT_TIMESTAMP,
                updated_at TIMESTAMP WITH TIME ZONE,
                CONSTRAINT fk_payments_order FOREIGN KEY (order_id) REFERENCES orders(id) ON DELETE RESTRICT
            )");

        // Slideshow table
        await connection.ExecuteAsync(@"
            CREATE TABLE IF NOT EXISTS slideshow (
                id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
                image_url TEXT NOT NULL,
                alt VARCHAR(255) NOT NULL,
                title VARCHAR(255),
                subtitle TEXT,
                cta_text VARCHAR(100),
                cta_link VARCHAR(500),
                ""order"" INTEGER NOT NULL DEFAULT 0,
                is_active BOOLEAN DEFAULT TRUE,
                created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT CURRENT_TIMESTAMP,
                updated_at TIMESTAMP WITH TIME ZONE
            )");

        // Create indexes
        await CreateIndexesAsync(connection);
    }

    private async Task RunMigrationsAsync(IDbConnection connection)
    {
        // Migration: Add cancellation tracking fields
        try
        {
            // Add cancellation_count to users table if it doesn't exist
            await connection.ExecuteAsync(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 FROM information_schema.columns 
                        WHERE table_name = 'users' AND column_name = 'cancellation_count'
                    ) THEN
                        ALTER TABLE users ADD COLUMN cancellation_count INTEGER NOT NULL DEFAULT 0;
                    END IF;
                END $$;");

            // Add order_blocked_until to users table if it doesn't exist
            await connection.ExecuteAsync(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 FROM information_schema.columns 
                        WHERE table_name = 'users' AND column_name = 'order_blocked_until'
                    ) THEN
                        ALTER TABLE users ADD COLUMN order_blocked_until TIMESTAMP WITH TIME ZONE;
                    END IF;
                END $$;");

            // Add cancellation_reason to orders table if it doesn't exist
            await connection.ExecuteAsync(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 FROM information_schema.columns 
                        WHERE table_name = 'orders' AND column_name = 'cancellation_reason'
                    ) THEN
                        ALTER TABLE orders ADD COLUMN cancellation_reason TEXT;
                    END IF;
                END $$;");

            // Migration: Add social login fields
            // Make password nullable for social login users
            await connection.ExecuteAsync(@"
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1 FROM information_schema.columns 
                        WHERE table_name = 'users' AND column_name = 'password' AND is_nullable = 'NO'
                    ) THEN
                        ALTER TABLE users ALTER COLUMN password DROP NOT NULL;
                    END IF;
                END $$;");

            // Add provider column if it doesn't exist
            await connection.ExecuteAsync(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 FROM information_schema.columns 
                        WHERE table_name = 'users' AND column_name = 'provider'
                    ) THEN
                        ALTER TABLE users ADD COLUMN provider VARCHAR(50);
                    END IF;
                END $$;");

            // Add provider_id column if it doesn't exist
            await connection.ExecuteAsync(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 FROM information_schema.columns 
                        WHERE table_name = 'users' AND column_name = 'provider_id'
                    ) THEN
                        ALTER TABLE users ADD COLUMN provider_id VARCHAR(255);
                    END IF;
                END $$;");

            // Migration: Add sale_price to products table
            await connection.ExecuteAsync(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 FROM information_schema.columns 
                        WHERE table_name = 'products' AND column_name = 'sale_price'
                    ) THEN
                        ALTER TABLE products ADD COLUMN sale_price DECIMAL(18, 2) 
                        CHECK (sale_price >= 0 AND (sale_price IS NULL OR sale_price < price));
                    END IF;
                END $$;");

            // Add unique constraint on provider + provider_id if it doesn't exist
            await connection.ExecuteAsync(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 FROM pg_constraint 
                        WHERE conname = 'unique_provider_user'
                    ) THEN
                        ALTER TABLE users ADD CONSTRAINT unique_provider_user UNIQUE (provider, provider_id);
                    END IF;
                END $$;");

            // Migration: Add colors and sizes arrays to products table
            await connection.ExecuteAsync(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 FROM information_schema.columns 
                        WHERE table_name = 'products' AND column_name = 'colors'
                    ) THEN
                        ALTER TABLE products ADD COLUMN colors TEXT[] DEFAULT ARRAY[]::TEXT[];
                    END IF;
                END $$;");

            await connection.ExecuteAsync(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 FROM information_schema.columns 
                        WHERE table_name = 'products' AND column_name = 'sizes'
                    ) THEN
                        ALTER TABLE products ADD COLUMN sizes INTEGER[] DEFAULT ARRAY[]::INTEGER[];
                    END IF;
                END $$;");

            // Migration: Add selected_color and selected_size to cart_items
            await connection.ExecuteAsync(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 FROM information_schema.columns 
                        WHERE table_name = 'cart_items' AND column_name = 'selected_color'
                    ) THEN
                        ALTER TABLE cart_items ADD COLUMN selected_color VARCHAR(50);
                    END IF;
                END $$;");

            await connection.ExecuteAsync(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 FROM information_schema.columns 
                        WHERE table_name = 'cart_items' AND column_name = 'selected_size'
                    ) THEN
                        ALTER TABLE cart_items ADD COLUMN selected_size INTEGER;
                    END IF;
                END $$;");

            // Migration: Add selected_color and selected_size to order_items
            await connection.ExecuteAsync(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 FROM information_schema.columns 
                        WHERE table_name = 'order_items' AND column_name = 'selected_color'
                    ) THEN
                        ALTER TABLE order_items ADD COLUMN selected_color VARCHAR(50);
                    END IF;
                END $$;");

            await connection.ExecuteAsync(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 FROM information_schema.columns 
                        WHERE table_name = 'order_items' AND column_name = 'selected_size'
                    ) THEN
                        ALTER TABLE order_items ADD COLUMN selected_size INTEGER;
                    END IF;
                END $$;");

            // Migration: Add phone to addresses table
            await connection.ExecuteAsync(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 FROM information_schema.columns 
                        WHERE table_name = 'addresses' AND column_name = 'phone'
                    ) THEN
                        ALTER TABLE addresses ADD COLUMN phone VARCHAR(30);
                    END IF;
                END $$;");

            // Migration: Add phone_number to users table
            await connection.ExecuteAsync(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 FROM information_schema.columns 
                        WHERE table_name = 'users' AND column_name = 'phone_number'
                    ) THEN
                        ALTER TABLE users ADD COLUMN phone_number VARCHAR(30);
                    END IF;
                END $$;");

            await connection.ExecuteAsync(@"
                CREATE TABLE IF NOT EXISTS stripe_webhook_events (
                    id VARCHAR(255) PRIMARY KEY,
                    event_type VARCHAR(120) NOT NULL,
                    received_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
                );");
            await connection.ExecuteAsync(
                "CREATE INDEX IF NOT EXISTS idx_stripe_webhook_events_received_at ON stripe_webhook_events (received_at);");

            await connection.ExecuteAsync(@"
                CREATE TABLE IF NOT EXISTS stripe_checkout_pending (
                    payment_intent_id VARCHAR(255) PRIMARY KEY,
                    payload_json TEXT NOT NULL,
                    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
                );");
            await connection.ExecuteAsync(
                "CREATE INDEX IF NOT EXISTS idx_stripe_checkout_pending_created_at ON stripe_checkout_pending (created_at);");

            await connection.ExecuteAsync(@"
                CREATE TABLE IF NOT EXISTS stripe_hosted_checkout_pending (
                    session_id VARCHAR(255) PRIMARY KEY,
                    payload_json TEXT NOT NULL,
                    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
                );");
            await connection.ExecuteAsync(
                "CREATE INDEX IF NOT EXISTS idx_stripe_hosted_checkout_pending_created_at ON stripe_hosted_checkout_pending (created_at);");

            await connection.ExecuteAsync(@"
                CREATE UNIQUE INDEX IF NOT EXISTS uq_orders_stripe_payment_intent_id
                ON orders (stripe_payment_intent_id)
                WHERE stripe_payment_intent_id IS NOT NULL;");
        }
        catch (Exception ex)
        {
            // Log error but don't fail startup
            Log.Warning(ex, "Migration error: {Message}", ex.Message);
        }
    }

    private async Task CreateIndexesAsync(IDbConnection connection)
    {
        var indexes = new[]
        {
            "CREATE INDEX IF NOT EXISTS idx_users_email ON users(email)",
            "CREATE INDEX IF NOT EXISTS idx_users_role ON users(role)",
            "CREATE INDEX IF NOT EXISTS idx_products_name ON products(name)",
            "CREATE INDEX IF NOT EXISTS idx_products_category ON products(category)",
            "CREATE INDEX IF NOT EXISTS idx_products_price ON products(price)",
            "CREATE INDEX IF NOT EXISTS idx_products_sku ON products(sku)",
            "CREATE INDEX IF NOT EXISTS idx_coupons_code ON coupons(code)",
            "CREATE INDEX IF NOT EXISTS idx_coupons_is_active ON coupons(is_active)",
            "CREATE INDEX IF NOT EXISTS idx_orders_user_id ON orders(user_id)",
            "CREATE INDEX IF NOT EXISTS idx_orders_status ON orders(status)",
            "CREATE INDEX IF NOT EXISTS idx_order_items_order_id ON order_items(order_id)",
            "            CREATE INDEX IF NOT EXISTS idx_order_items_product_id ON order_items(product_id)",
            "CREATE INDEX IF NOT EXISTS idx_payments_order_id ON payments(order_id)",
            "CREATE INDEX IF NOT EXISTS idx_payments_stripe_payment_id ON payments(stripe_payment_id)",
            "CREATE INDEX IF NOT EXISTS idx_addresses_user_id ON addresses(user_id)",
            "CREATE INDEX IF NOT EXISTS idx_cart_items_user_id ON cart_items(user_id)",
            "CREATE INDEX IF NOT EXISTS idx_cart_items_product_id ON cart_items(product_id)",
            "CREATE INDEX IF NOT EXISTS idx_slideshow_is_active ON slideshow(is_active)",
            "CREATE INDEX IF NOT EXISTS idx_slideshow_order ON slideshow(\"order\")"
        };

        foreach (var index in indexes)
        {
            try
            {
                await connection.ExecuteAsync(index);
            }
            catch
            {
                // Index might already exist
            }
        }
    }
}
