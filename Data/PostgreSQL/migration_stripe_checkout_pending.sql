-- Deferred Stripe checkout: snapshot stored at PaymentIntent creation; order/stock/cart applied on payment success.
CREATE TABLE IF NOT EXISTS stripe_checkout_pending (
    payment_intent_id VARCHAR(255) PRIMARY KEY,
    payload_json TEXT NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_stripe_checkout_pending_created_at ON stripe_checkout_pending (created_at);

-- Prevent duplicate orders if webhook and confirm race (PostgreSQL partial unique index).
CREATE UNIQUE INDEX IF NOT EXISTS uq_orders_stripe_payment_intent_id
    ON orders (stripe_payment_intent_id)
    WHERE stripe_payment_intent_id IS NOT NULL;

COMMENT ON TABLE stripe_checkout_pending IS 'Cart/order snapshot for Stripe PI until payment_intent.succeeded';
