-- Hosted Stripe Checkout: cart snapshot keyed by Checkout Session id until session completes and migrates to stripe_checkout_pending (by payment_intent_id).

CREATE TABLE IF NOT EXISTS stripe_hosted_checkout_pending (
    session_id VARCHAR(255) PRIMARY KEY,
    payload_json TEXT NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_stripe_hosted_checkout_pending_created_at ON stripe_hosted_checkout_pending (created_at);

COMMENT ON TABLE stripe_hosted_checkout_pending IS 'Cart snapshot for Stripe Hosted Checkout until checkout.session.completed / PI migration';
