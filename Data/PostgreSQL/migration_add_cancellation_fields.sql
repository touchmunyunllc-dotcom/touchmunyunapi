-- Migration: Add cancellation tracking fields
-- Date: 2025-01-XX
-- Description: Adds cancellation_count and order_blocked_until to users table,
--              and cancellation_reason to orders table

-- Add cancellation tracking fields to users table
DO $$
BEGIN
    -- Add cancellation_count if it doesn't exist
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns 
        WHERE table_name = 'users' AND column_name = 'cancellation_count'
    ) THEN
        ALTER TABLE users ADD COLUMN cancellation_count INTEGER NOT NULL DEFAULT 0;
    END IF;

    -- Add order_blocked_until if it doesn't exist
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns 
        WHERE table_name = 'users' AND column_name = 'order_blocked_until'
    ) THEN
        ALTER TABLE users ADD COLUMN order_blocked_until TIMESTAMP WITH TIME ZONE;
    END IF;
END $$;

-- Add cancellation_reason to orders table
DO $$
BEGIN
    -- Add cancellation_reason if it doesn't exist
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns 
        WHERE table_name = 'orders' AND column_name = 'cancellation_reason'
    ) THEN
        ALTER TABLE orders ADD COLUMN cancellation_reason TEXT;
    END IF;
END $$;

-- Verify the changes
SELECT 
    column_name, 
    data_type, 
    is_nullable,
    column_default
FROM information_schema.columns 
WHERE table_name IN ('users', 'orders') 
    AND column_name IN ('cancellation_count', 'order_blocked_until', 'cancellation_reason')
ORDER BY table_name, column_name;

