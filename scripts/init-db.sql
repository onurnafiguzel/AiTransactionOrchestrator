-- PostgreSQL initialization script
-- Creates additional databases and schemas if needed

-- Enable required extensions
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
CREATE EXTENSION IF NOT EXISTS "pgcrypto";

create table if not exists transaction_timeline (
  id uuid primary key,
  transaction_id uuid not null,
  event_type text not null,
  details_json text null,
  occurred_at_utc timestamptz not null,
  correlation_id text null,
  source text null
);

create index if not exists ix_transaction_timeline_tx_time
  on transaction_timeline (transaction_id, occurred_at_utc desc);

create index if not exists ix_transaction_timeline_event_type
  on transaction_timeline (event_type);

-- Grant permissions
GRANT USAGE ON SCHEMA transactions TO ato;
GRANT USAGE ON SCHEMA outbox TO ato;
GRANT USAGE ON SCHEMA inbox TO ato;