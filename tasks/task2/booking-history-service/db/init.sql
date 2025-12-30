CREATE TABLE IF NOT EXISTS booking_history (
    booking_id TEXT PRIMARY KEY,
    user_id TEXT NOT NULL,
    hotel_id TEXT NOT NULL,
    promo_code TEXT,
    discount_percent DOUBLE PRECISION NOT NULL,
    price DOUBLE PRECISION NOT NULL,
    created_at TIMESTAMPTZ NOT NULL
);

CREATE TABLE IF NOT EXISTS booking_stats_user (
    user_id TEXT PRIMARY KEY,
    total_bookings INT NOT NULL,
    total_spent DOUBLE PRECISION NOT NULL
);

CREATE TABLE IF NOT EXISTS booking_stats_hotel (
    hotel_id TEXT PRIMARY KEY,
    total_bookings INT NOT NULL,
    total_revenue DOUBLE PRECISION NOT NULL
);

CREATE TABLE IF NOT EXISTS booking_stats_day (
    day DATE PRIMARY KEY,
    total_bookings INT NOT NULL,
    total_revenue DOUBLE PRECISION NOT NULL
);
