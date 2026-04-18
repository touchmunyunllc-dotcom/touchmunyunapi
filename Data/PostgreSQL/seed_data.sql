-- Seed Data for E-Commerce Application
-- Includes: 5 users, 5 products, 2 coupons, sample addresses

-- ============================================
-- SEED USERS (5 users)
-- ============================================
-- Note: Passwords should be hashed using BCrypt in the application code
-- These are placeholder hashes. The SeedData.cs will generate proper BCrypt hashes.
-- Default passwords:
-- Admin: Admin@123
-- Customers: Password123!
INSERT INTO users (id, name, email, password, role, created_at) VALUES
('11111111-1111-1111-1111-111111111111', 'Admin User', 'admin@ecommerce.com', '$2a$11$placeholder_hash_will_be_replaced', 'Admin', CURRENT_TIMESTAMP),
('22222222-2222-2222-2222-222222222222', 'John Doe', 'john.doe@example.com', '$2a$11$placeholder_hash_will_be_replaced', 'Customer', CURRENT_TIMESTAMP),
('33333333-3333-3333-3333-333333333333', 'Jane Smith', 'jane.smith@example.com', '$2a$11$placeholder_hash_will_be_replaced', 'Customer', CURRENT_TIMESTAMP),
('44444444-4444-4444-4444-444444444444', 'Bob Johnson', 'bob.johnson@example.com', '$2a$11$placeholder_hash_will_be_replaced', 'Customer', CURRENT_TIMESTAMP),
('55555555-5555-5555-5555-555555555555', 'Alice Williams', 'alice.williams@example.com', '$2a$11$placeholder_hash_will_be_replaced', 'Customer', CURRENT_TIMESTAMP)
ON CONFLICT (email) DO NOTHING;

-- Note: Actual BCrypt password hashing is done in SeedData.cs using BCrypt.Net
-- The application will update these passwords with proper hashes on first run

-- ============================================
-- SEED ADDRESSES
-- ============================================
INSERT INTO addresses (id, user_id, address_line1, address_line2, city, state, postal_code, country, is_default, created_at) VALUES
('a1111111-1111-1111-1111-111111111111', '22222222-2222-2222-2222-222222222222', '123 Main Street', 'Apt 4B', 'New York', 'NY', '10001', 'United States', TRUE, CURRENT_TIMESTAMP),
('a2222222-2222-2222-2222-222222222222', '33333333-3333-3333-3333-333333333333', '456 Oak Avenue', NULL, 'Los Angeles', 'CA', '90001', 'United States', TRUE, CURRENT_TIMESTAMP),
('a3333333-3333-3333-3333-333333333333', '44444444-4444-4444-4444-444444444444', '789 Pine Road', 'Suite 200', 'Chicago', 'IL', '60601', 'United States', TRUE, CURRENT_TIMESTAMP),
('a4444444-4444-4444-4444-444444444444', '55555555-5555-5555-5555-555555555555', '321 Elm Street', NULL, 'Houston', 'TX', '77001', 'United States', TRUE, CURRENT_TIMESTAMP)
ON CONFLICT DO NOTHING;

-- ============================================
-- SEED PRODUCTS (5 products)
-- ============================================
INSERT INTO products (id, name, description, price, images, available_quantity, category, sku, is_active, created_at, updated_at) VALUES
('p1111111-1111-1111-1111-111111111111', 
 'Wireless Bluetooth Headphones', 
 'Premium noise-cancelling wireless headphones with 30-hour battery life and superior sound quality. Perfect for music lovers and professionals.',
 129.99,
 ARRAY['https://images.unsplash.com/photo-1505740420928-5e560c06d30e?w=500', 'https://images.unsplash.com/photo-1484704849700-f032a568e944?w=500'],
 150,
 'Electronics',
 'ELEC-001',
 TRUE,
 CURRENT_TIMESTAMP,
 CURRENT_TIMESTAMP),

('p2222222-2222-2222-2222-222222222222',
 'Smart Fitness Watch',
 'Advanced fitness tracking watch with heart rate monitor, GPS, and 7-day battery life. Track your workouts and health metrics.',
 249.99,
 ARRAY['https://images.unsplash.com/photo-1523275335684-37898b6baf30?w=500', 'https://images.unsplash.com/photo-1579586337278-3befd40fd17a?w=500'],
 75,
 'Electronics',
 'ELEC-002',
 TRUE,
 CURRENT_TIMESTAMP,
 CURRENT_TIMESTAMP),

('p3333333-3333-3333-3333-333333333333',
 'Organic Cotton T-Shirt',
 'Comfortable and sustainable organic cotton t-shirt. Available in multiple colors. Perfect for everyday wear.',
 29.99,
 ARRAY['https://images.unsplash.com/photo-1521572163474-6864f9cf17ab?w=500', 'https://images.unsplash.com/photo-1583743814966-8936f5b7be1a?w=500'],
 200,
 'Clothing',
 'CLOTH-001',
 TRUE,
 CURRENT_TIMESTAMP,
 CURRENT_TIMESTAMP),

('p4444444-4444-4444-4444-444444444444',
 'Stainless Steel Water Bottle',
 'Eco-friendly 32oz insulated water bottle keeps drinks cold for 24 hours or hot for 12 hours. BPA-free and dishwasher safe.',
 34.99,
 ARRAY['https://images.unsplash.com/photo-1602143407151-7111542de6e8?w=500', 'https://images.unsplash.com/photo-1602143407151-7111542de6e8?w=500'],
 300,
 'Accessories',
 'ACC-001',
 TRUE,
 CURRENT_TIMESTAMP,
 CURRENT_TIMESTAMP),

('p5555555-5555-5555-5555-555555555555',
 'Premium Coffee Beans',
 'Artisan roasted coffee beans from Colombia. Medium roast with notes of chocolate and caramel. 1lb bag.',
 24.99,
 ARRAY['https://images.unsplash.com/photo-1559056199-641a0ac8b55e?w=500', 'https://images.unsplash.com/photo-1511920170033-f8396924c348?w=500'],
 100,
 'Food & Beverage',
 'FOOD-001',
 TRUE,
 CURRENT_TIMESTAMP,
 CURRENT_TIMESTAMP)
ON CONFLICT (sku) DO NOTHING;

-- ============================================
-- SEED COUPONS (2 coupons)
-- ============================================
INSERT INTO coupons (id, code, discount_type, discount_value, expiry_date, usage_limit, usage_count, is_active, min_purchase_amount, max_discount_amount, created_at) VALUES
('c1111111-1111-1111-1111-111111111111',
 'WELCOME10',
 'Percentage',
 10.00,
 CURRENT_TIMESTAMP + INTERVAL '30 days',
 100,
 0,
 TRUE,
 50.00,
 25.00,
 CURRENT_TIMESTAMP),

('c2222222-2222-2222-2222-222222222222',
 'SAVE20',
 'Percentage',
 20.00,
 CURRENT_TIMESTAMP + INTERVAL '60 days',
 50,
 0,
 TRUE,
 100.00,
 50.00,
 CURRENT_TIMESTAMP)
ON CONFLICT (code) DO NOTHING;

-- ============================================
-- VERIFICATION QUERIES
-- ============================================
-- Run these to verify seed data:
-- SELECT COUNT(*) FROM users; -- Should return 5
-- SELECT COUNT(*) FROM products; -- Should return 5
-- SELECT COUNT(*) FROM coupons; -- Should return 2
-- SELECT COUNT(*) FROM addresses; -- Should return 4

