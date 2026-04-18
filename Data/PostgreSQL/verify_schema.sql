-- Verification Queries for PostgreSQL Schema
-- Run these queries to verify the database schema and seed data

-- ============================================
-- TABLE COUNTS
-- ============================================
SELECT 'Users' as table_name, COUNT(*) as count FROM users
UNION ALL
SELECT 'Products', COUNT(*) FROM products
UNION ALL
SELECT 'Coupons', COUNT(*) FROM coupons
UNION ALL
SELECT 'Addresses', COUNT(*) FROM addresses
UNION ALL
SELECT 'Orders', COUNT(*) FROM orders
UNION ALL
SELECT 'Order Items', COUNT(*) FROM order_items
UNION ALL
SELECT 'Payments', COUNT(*) FROM payments;

-- ============================================
-- VERIFY USERS (Should be 5)
-- ============================================
SELECT id, name, email, role, created_at FROM users ORDER BY created_at;

-- ============================================
-- VERIFY PRODUCTS (Should be 5)
-- ============================================
SELECT id, name, price, available_quantity, category, array_length(images, 1) as image_count FROM products ORDER BY created_at;

-- ============================================
-- VERIFY COUPONS (Should be 2)
-- ============================================
SELECT id, code, discount_type, discount_value, expiry_date, usage_limit, is_active FROM coupons ORDER BY created_at;

-- ============================================
-- VERIFY ADDRESSES (Should be 4)
-- ============================================
SELECT id, user_id, city, state, postal_code, is_default FROM addresses ORDER BY created_at;

-- ============================================
-- VERIFY INDEXES
-- ============================================
SELECT 
    schemaname,
    tablename,
    indexname,
    indexdef
FROM pg_indexes
WHERE schemaname = 'public'
ORDER BY tablename, indexname;

-- ============================================
-- VERIFY FOREIGN KEYS
-- ============================================
SELECT
    tc.table_name, 
    kcu.column_name, 
    ccu.table_name AS foreign_table_name,
    ccu.column_name AS foreign_column_name 
FROM information_schema.table_constraints AS tc 
JOIN information_schema.key_column_usage AS kcu
    ON tc.constraint_name = kcu.constraint_name
    AND tc.table_schema = kcu.table_schema
JOIN information_schema.constraint_column_usage AS ccu
    ON ccu.constraint_name = tc.constraint_name
    AND ccu.table_schema = tc.table_schema
WHERE tc.constraint_type = 'FOREIGN KEY'
ORDER BY tc.table_name;

-- ============================================
-- VERIFY CONSTRAINTS
-- ============================================
SELECT
    conname AS constraint_name,
    contype AS constraint_type,
    conrelid::regclass AS table_name
FROM pg_constraint
WHERE connamespace = 'public'::regnamespace
ORDER BY conrelid::regclass::text, contype, conname;

