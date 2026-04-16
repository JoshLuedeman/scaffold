-- PostgreSQL seed data for Scaffold demo
-- This creates a sample database with various PG features for assessment testing

-- Create schemas
CREATE SCHEMA IF NOT EXISTS inventory;
CREATE SCHEMA IF NOT EXISTS analytics;

-- Enable common extensions
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
CREATE EXTENSION IF NOT EXISTS "pg_trgm";
CREATE EXTENSION IF NOT EXISTS "hstore";

-- Tables with various data types
CREATE TABLE inventory.products (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    name VARCHAR(255) NOT NULL,
    description TEXT,
    price NUMERIC(10, 2) NOT NULL DEFAULT 0.00,
    metadata JSONB DEFAULT '{}',
    tags TEXT[] DEFAULT '{}',
    created_at TIMESTAMPTZ DEFAULT NOW(),
    updated_at TIMESTAMPTZ DEFAULT NOW()
);

CREATE TABLE inventory.categories (
    id SERIAL PRIMARY KEY,
    name VARCHAR(100) NOT NULL UNIQUE,
    parent_id INTEGER REFERENCES inventory.categories(id),
    attributes HSTORE
);

CREATE TABLE inventory.product_categories (
    product_id UUID REFERENCES inventory.products(id) ON DELETE CASCADE,
    category_id INTEGER REFERENCES inventory.categories(id) ON DELETE CASCADE,
    PRIMARY KEY (product_id, category_id)
);

CREATE TABLE analytics.page_views (
    id BIGSERIAL PRIMARY KEY,
    product_id UUID REFERENCES inventory.products(id),
    viewer_ip INET,
    user_agent TEXT,
    viewed_at TIMESTAMPTZ DEFAULT NOW()
) PARTITION BY RANGE (viewed_at);

-- Create partitions
CREATE TABLE analytics.page_views_2025_q1 PARTITION OF analytics.page_views
    FOR VALUES FROM ('2025-01-01') TO ('2025-04-01');
CREATE TABLE analytics.page_views_2025_q2 PARTITION OF analytics.page_views
    FOR VALUES FROM ('2025-04-01') TO ('2025-07-01');

-- Indexes
CREATE INDEX idx_products_name_trgm ON inventory.products USING gin (name gin_trgm_ops);
CREATE INDEX idx_products_metadata ON inventory.products USING gin (metadata);
CREATE INDEX idx_products_tags ON inventory.products USING gin (tags);
CREATE INDEX idx_page_views_product ON analytics.page_views (product_id);

-- Views
CREATE VIEW inventory.product_summary AS
SELECT p.id, p.name, p.price, COUNT(pc.category_id) as category_count
FROM inventory.products p
LEFT JOIN inventory.product_categories pc ON p.id = pc.product_id
GROUP BY p.id, p.name, p.price;

-- Materialized view
CREATE MATERIALIZED VIEW analytics.daily_views AS
SELECT product_id, DATE(viewed_at) as view_date, COUNT(*) as view_count
FROM analytics.page_views
GROUP BY product_id, DATE(viewed_at);

-- Function
CREATE OR REPLACE FUNCTION inventory.update_timestamp()
RETURNS TRIGGER AS $$
BEGIN
    NEW.updated_at = NOW();
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

-- Trigger
CREATE TRIGGER trg_products_update
    BEFORE UPDATE ON inventory.products
    FOR EACH ROW
    EXECUTE FUNCTION inventory.update_timestamp();

-- Sequence (explicit)
CREATE SEQUENCE inventory.order_seq START WITH 1000 INCREMENT BY 1;

-- Insert seed data
INSERT INTO inventory.categories (name, parent_id, attributes) VALUES
    ('Electronics', NULL, 'brand => "Various", warranty => "1 year"'),
    ('Laptops', 1, 'form_factor => "portable"'),
    ('Accessories', 1, NULL);

INSERT INTO inventory.products (name, description, price, metadata, tags) VALUES
    ('Developer Laptop', 'High-performance development machine', 1299.99, 
     '{"cpu": "i7", "ram": "32GB", "storage": "1TB SSD"}', ARRAY['laptop', 'dev', 'high-end']),
    ('USB-C Hub', 'Multi-port USB-C adapter', 49.99,
     '{"ports": 7, "power_delivery": true}', ARRAY['accessory', 'usb-c']),
    ('Mechanical Keyboard', 'Cherry MX Blue switches', 129.99,
     '{"switches": "Cherry MX Blue", "layout": "TKL"}', ARRAY['keyboard', 'mechanical']);
