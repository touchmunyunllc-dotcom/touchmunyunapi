using ECommerce.Models;
using System.Data;
using Dapper;
using BCrypt.Net;
using Npgsql;

namespace ECommerce.Data;

public static class SeedData
{
    public static async Task InitializeAsync(IServiceProvider serviceProvider)
    {
        var connection = serviceProvider.GetRequiredService<IDbConnection>();
        if (connection.State != ConnectionState.Open)
        {
            connection.Open();
        }

        try
        {
            // Seed users if none exist (5 users)
            var userCount = await connection.QueryFirstOrDefaultAsync<int>("SELECT COUNT(*) FROM users");
            if (userCount == 0)
            {
                var users = new[]
                {
                    new { Id = Guid.Parse("11111111-1111-1111-1111-111111111111"), Email = "admin@ecommerce.com", Name = "Admin User", PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin@123"), Role = "Admin" },
                    new { Id = Guid.Parse("22222222-2222-2222-2222-222222222222"), Email = "john.doe@example.com", Name = "John Doe", PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password123!"), Role = "Customer" },
                    new { Id = Guid.Parse("33333333-3333-3333-3333-333333333333"), Email = "jane.smith@example.com", Name = "Jane Smith", PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password123!"), Role = "Customer" },
                    new { Id = Guid.Parse("44444444-4444-4444-4444-444444444444"), Email = "bob.johnson@example.com", Name = "Bob Johnson", PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password123!"), Role = "Customer" },
                    new { Id = Guid.Parse("55555555-5555-5555-5555-555555555555"), Email = "alice.williams@example.com", Name = "Alice Williams", PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password123!"), Role = "Customer" }
                };

                foreach (var user in users)
                {
                    await connection.ExecuteAsync(@"
                        INSERT INTO users (id, email, name, password, role, created_at)
                        VALUES (@Id, @Email, @Name, @PasswordHash, @Role, CURRENT_TIMESTAMP)
                        ON CONFLICT (email) DO NOTHING",
                        user);
                }
                Console.WriteLine("✓ Seeded 5 users");
            }

            // Seed addresses
            var addressCount = await connection.QueryFirstOrDefaultAsync<int>("SELECT COUNT(*) FROM addresses");
            if (addressCount == 0)
            {
                var addresses = new[]
                {
                    new { Id = Guid.Parse("a1111111-1111-1111-1111-111111111111"), UserId = Guid.Parse("22222222-2222-2222-2222-222222222222"), AddressLine1 = "123 Main Street", AddressLine2 = "Apt 4B", City = "New York", State = "NY", PostalCode = "10001", Country = "United States", IsDefault = true },
                    new { Id = Guid.Parse("a2222222-2222-2222-2222-222222222222"), UserId = Guid.Parse("33333333-3333-3333-3333-333333333333"), AddressLine1 = "456 Oak Avenue", AddressLine2 = (string?)null, City = "Los Angeles", State = "CA", PostalCode = "90001", Country = "United States", IsDefault = true },
                    new { Id = Guid.Parse("a3333333-3333-3333-3333-333333333333"), UserId = Guid.Parse("44444444-4444-4444-4444-444444444444"), AddressLine1 = "789 Pine Road", AddressLine2 = "Suite 200", City = "Chicago", State = "IL", PostalCode = "60601", Country = "United States", IsDefault = true },
                    new { Id = Guid.Parse("a4444444-4444-4444-4444-444444444444"), UserId = Guid.Parse("55555555-5555-5555-5555-555555555555"), AddressLine1 = "321 Elm Street", AddressLine2 = (string?)null, City = "Houston", State = "TX", PostalCode = "77001", Country = "United States", IsDefault = true }
                };

                foreach (var address in addresses)
                {
                    await connection.ExecuteAsync(@"
                        INSERT INTO addresses (id, user_id, address_line1, address_line2, city, state, postal_code, country, is_default, created_at)
                        VALUES (@Id, @UserId, @AddressLine1, @AddressLine2, @City, @State, @PostalCode, @Country, @IsDefault, CURRENT_TIMESTAMP)
                        ON CONFLICT DO NOTHING",
                        address);
                }
                Console.WriteLine("✓ Seeded 4 addresses");
            }

            // Seed products if none exist (5 products)
            var productCount = await connection.QueryFirstOrDefaultAsync<int>("SELECT COUNT(*) FROM products");
            if (productCount == 0)
            {
                var products = new[]
                {
                    new
                    {
                        Id = Guid.Parse("e1111111-1111-1111-1111-111111111111"),
                        Name = "Wireless Bluetooth Headphones",
                        Description = "Premium noise-cancelling wireless headphones with 30-hour battery life and superior sound quality. Perfect for music lovers and professionals.",
                        Price = 129.99m,
                        Images = new[] { "https://images.unsplash.com/photo-1505740420928-5e560c06d30e?w=500", "https://images.unsplash.com/photo-1484704849700-f032a568e944?w=500" },
                        AvailableQuantity = 150,
                        Category = "Electronics",
                        Sku = "ELEC-001",
                        IsActive = true
                    },
                    new
                    {
                        Id = Guid.Parse("e2222222-2222-2222-2222-222222222222"),
                        Name = "Smart Fitness Watch",
                        Description = "Advanced fitness tracking watch with heart rate monitor, GPS, and 7-day battery life. Track your workouts and health metrics.",
                        Price = 249.99m,
                        Images = new[] { "https://images.unsplash.com/photo-1523275335684-37898b6baf30?w=500", "https://images.unsplash.com/photo-1579586337278-3befd40fd17a?w=500" },
                        AvailableQuantity = 75,
                        Category = "Electronics",
                        Sku = "ELEC-002",
                        IsActive = true
                    },
                    new
                    {
                        Id = Guid.Parse("e3333333-3333-3333-3333-333333333333"),
                        Name = "Organic Cotton T-Shirt",
                        Description = "Comfortable and sustainable organic cotton t-shirt. Available in multiple colors. Perfect for everyday wear.",
                        Price = 29.99m,
                        Images = new[] { "https://images.unsplash.com/photo-1521572163474-6864f9cf17ab?w=500", "https://images.unsplash.com/photo-1583743814966-8936f5b7be1a?w=500" },
                        AvailableQuantity = 200,
                        Category = "Clothing",
                        Sku = "CLOTH-001",
                        IsActive = true
                    },
                    new
                    {
                        Id = Guid.Parse("e4444444-4444-4444-4444-444444444444"),
                        Name = "Stainless Steel Water Bottle",
                        Description = "Eco-friendly 32oz insulated water bottle keeps drinks cold for 24 hours or hot for 12 hours. BPA-free and dishwasher safe.",
                        Price = 34.99m,
                        Images = new[] { "https://images.unsplash.com/photo-1602143407151-7111542de6e8?w=500" },
                        AvailableQuantity = 300,
                        Category = "Accessories",
                        Sku = "ACC-001",
                        IsActive = true
                    },
                    new
                    {
                        Id = Guid.Parse("e5555555-5555-5555-5555-555555555555"),
                        Name = "Premium Coffee Beans",
                        Description = "Artisan roasted coffee beans from Colombia. Medium roast with notes of chocolate and caramel. 1lb bag.",
                        Price = 24.99m,
                        Images = new[] { "https://images.unsplash.com/photo-1559056199-641a0ac8b55e?w=500", "https://images.unsplash.com/photo-1511920170033-f8396924c348?w=500" },
                        AvailableQuantity = 100,
                        Category = "Food & Beverage",
                        Sku = "FOOD-001",
                        IsActive = true
                    }
                };

                foreach (var product in products)
                {
                    // Convert string array to PostgreSQL array format
                    var imagesArray = product.Images.ToArray();
                    await connection.ExecuteAsync(@"
                        INSERT INTO products (id, name, description, price, images, available_quantity, category, sku, is_active, created_at, updated_at)
                        VALUES (@Id, @Name, @Description, @Price, @Images, @AvailableQuantity, @Category, @Sku, @IsActive, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP)
                        ON CONFLICT (sku) DO NOTHING",
                        new
                        {
                            product.Id,
                            product.Name,
                            product.Description,
                            product.Price,
                            Images = imagesArray, // PostgreSQL array
                            product.AvailableQuantity,
                            product.Category,
                            product.Sku,
                            product.IsActive
                        });
                }
                Console.WriteLine("✓ Seeded 5 products");
            }

            // Seed coupons if none exist (2 coupons)
            var couponCount = await connection.QueryFirstOrDefaultAsync<int>("SELECT COUNT(*) FROM coupons");
            if (couponCount == 0)
            {
                var coupons = new[]
                {
                    new
                    {
                        Id = Guid.Parse("c1111111-1111-1111-1111-111111111111"),
                        Code = "WELCOME10",
                        DiscountType = "Percentage",
                        DiscountValue = 10.00m,
                        ExpiryDate = DateTime.UtcNow.AddDays(30),
                        UsageLimit = 100,
                        UsageCount = 0,
                        IsActive = true,
                        MinPurchaseAmount = 50.00m,
                        MaxDiscountAmount = 25.00m
                    },
                    new
                    {
                        Id = Guid.Parse("c2222222-2222-2222-2222-222222222222"),
                        Code = "SAVE20",
                        DiscountType = "Percentage",
                        DiscountValue = 20.00m,
                        ExpiryDate = DateTime.UtcNow.AddDays(60),
                        UsageLimit = 50,
                        UsageCount = 0,
                        IsActive = true,
                        MinPurchaseAmount = 100.00m,
                        MaxDiscountAmount = 50.00m
                    }
                };

                foreach (var coupon in coupons)
                {
                    await connection.ExecuteAsync(@"
                        INSERT INTO coupons (id, code, discount_type, discount_value, expiry_date, usage_limit, usage_count, is_active, min_purchase_amount, max_discount_amount, created_at)
                        VALUES (@Id, @Code, @DiscountType, @DiscountValue, @ExpiryDate, @UsageLimit, @UsageCount, @IsActive, @MinPurchaseAmount, @MaxDiscountAmount, CURRENT_TIMESTAMP)
                        ON CONFLICT (code) DO NOTHING",
                        coupon);
                }
                Console.WriteLine("✓ Seeded 2 coupons");
            }

            Console.WriteLine("✓ Database seeding completed successfully");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Error seeding database: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            throw;
        }
    }
}
