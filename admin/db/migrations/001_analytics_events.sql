CREATE TABLE IF NOT EXISTS analytics_events (
  id BIGSERIAL PRIMARY KEY,
  installation_id UUID NOT NULL,
  event_name TEXT NOT NULL,
  app_version TEXT NOT NULL,
  platform TEXT NOT NULL,
  country_code CHAR(2),
  occurred_at TIMESTAMPTZ NOT NULL,
  event_day DATE NOT NULL,
  properties JSONB NOT NULL DEFAULT '{}'::jsonb,
  CONSTRAINT analytics_events_event_day_utc_check
    CHECK (event_day = (occurred_at AT TIME ZONE 'UTC')::date)
);

CREATE INDEX IF NOT EXISTS analytics_events_event_day_idx
  ON analytics_events (event_day);

CREATE INDEX IF NOT EXISTS analytics_events_installation_event_day_idx
  ON analytics_events (installation_id, event_day);

CREATE INDEX IF NOT EXISTS analytics_events_event_name_idx
  ON analytics_events (event_name);

CREATE INDEX IF NOT EXISTS analytics_events_country_code_idx
  ON analytics_events (country_code);
