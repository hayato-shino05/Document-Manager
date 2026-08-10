import type { SqlClient } from "./db.ts";

export type AnalyticsEventInput = {
  installationId: string;
  eventName: string;
  appVersion: string;
  platform: string;
  countryCode?: string | null;
  occurredAt: Date;
  properties?: Record<string, unknown>;
  dailyIdempotent?: boolean;
};

export type AnalyticsOverview = {
  eventsToday: number;
  dailyActiveInstallations: number;
  weeklyActiveInstallations: number;
  monthlyActiveInstallations: number;
  lastEventAt: Date | null;
};

export type MonthlyReportRow = {
  day: string;
  eventCount: number;
  activeInstallations: number;
};

export type MonthlyAggregateRow = {
  month: string;
  eventCount: number;
  activeInstallations: number;
};

export type AnalyticsBreakdown = {
  version: Array<{ key: string; count: number }>;
  platform: Array<{ key: string; count: number }>;
  country: Array<{ key: string; count: number }>;
};

type OverviewRow = {
  events_today: number | string;
  daily_active_installations: number | string;
  weekly_active_installations: number | string;
  monthly_active_installations: number | string;
  last_event_at: string | Date | null;
};

type MonthlyRow = {
  day: string;
  event_count: number | string;
  active_installations: number | string;
};

type MonthlyAggregateSqlRow = {
  month: string;
  event_count: number | string;
  active_installations: number | string;
};

type BreakdownSqlRow = {
  dimension: string;
  key: string | null;
  count: number | string;
};

const UUID_PATTERN = /^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i;

function utcDate(value: Date): string {
  return value.toISOString().slice(0, 10);
}

function dayStart(value: Date): Date {
  return new Date(Date.UTC(value.getUTCFullYear(), value.getUTCMonth(), value.getUTCDate()));
}

function addDays(value: Date, days: number): Date {
  const result = new Date(value);
  result.setUTCDate(result.getUTCDate() + days);
  return result;
}

function asCount(value: number | string): number {
  const result = Number(value);
  if (!Number.isSafeInteger(result) || result < 0) {
    throw new Error("Analytics count is outside the supported range");
  }
  return result;
}

function validateEvent(input: AnalyticsEventInput): void {
  if (!UUID_PATTERN.test(input.installationId)) {
    throw new TypeError("installationId must be a UUID");
  }
  if (!input.eventName.trim() || !input.appVersion.trim() || !input.platform.trim()) {
    throw new TypeError("eventName, appVersion, and platform are required");
  }
  if (!(input.occurredAt instanceof Date) || Number.isNaN(input.occurredAt.getTime())) {
    throw new TypeError("occurredAt must be a valid Date");
  }
  if (input.countryCode !== undefined && input.countryCode !== null && !/^[A-Za-z]{2}$/.test(input.countryCode)) {
    throw new TypeError("countryCode must contain two letters");
  }
  if (input.properties !== undefined && (input.properties === null || Array.isArray(input.properties))) {
    throw new TypeError("properties must be a JSON object");
  }
}

export class AnalyticsRepository {
  private readonly sql: SqlClient;

  constructor(sql: SqlClient) {
    this.sql = sql;
  }

  async insertEvent(input: AnalyticsEventInput): Promise<boolean> {
    validateEvent(input);

    const eventDay = utcDate(input.occurredAt);
    const properties = JSON.stringify(input.properties ?? {});
    const countryCode = input.countryCode?.toUpperCase() ?? null;

    if (input.dailyIdempotent) {
      const rows = await this.sql<{ id: string }[]>`
        INSERT INTO analytics_events (
          installation_id, event_name, app_version, platform, country_code,
          occurred_at, event_day, properties
        )
        SELECT
          ${input.installationId}::uuid, ${input.eventName}, ${input.appVersion}, ${input.platform},
          ${countryCode}, ${input.occurredAt.toISOString()}::timestamptz, ${eventDay}::date,
          ${properties}::jsonb
        WHERE NOT EXISTS (
          SELECT 1
          FROM analytics_events
          WHERE installation_id = ${input.installationId}::uuid
            AND event_name = ${input.eventName}
            AND event_day = ${eventDay}::date
        )
        RETURNING id
      `;
      return rows.length > 0;
    }

    await this.sql`
      INSERT INTO analytics_events (
        installation_id, event_name, app_version, platform, country_code,
        occurred_at, event_day, properties
      ) VALUES (
        ${input.installationId}::uuid, ${input.eventName}, ${input.appVersion}, ${input.platform},
        ${countryCode}, ${input.occurredAt.toISOString()}::timestamptz, ${eventDay}::date,
        ${properties}::jsonb
      )
    `;
    return true;
  }

  async getOverview(at: Date = new Date()): Promise<AnalyticsOverview> {
    if (!(at instanceof Date) || Number.isNaN(at.getTime())) {
      throw new TypeError("at must be a valid Date");
    }

    const today = dayStart(at);
    const tomorrow = addDays(today, 1);
    const sevenDayStart = addDays(today, -6);
    const monthStart = new Date(Date.UTC(today.getUTCFullYear(), today.getUTCMonth(), 1));

    const rows = await this.sql<OverviewRow[]>`
      SELECT
        COUNT(*) FILTER (WHERE event_day = ${utcDate(today)}::date)::int AS events_today,
        COUNT(DISTINCT installation_id) FILTER (
          WHERE event_day = ${utcDate(today)}::date
        )::int AS daily_active_installations,
        COUNT(DISTINCT installation_id) FILTER (
          WHERE event_day >= ${utcDate(sevenDayStart)}::date
            AND event_day < ${utcDate(tomorrow)}::date
        )::int AS weekly_active_installations,
        COUNT(DISTINCT installation_id) FILTER (
          WHERE event_day >= ${utcDate(monthStart)}::date
            AND event_day < ${utcDate(tomorrow)}::date
        )::int AS monthly_active_installations,
        MAX(occurred_at) AS last_event_at
      FROM analytics_events
      WHERE event_day >= ${utcDate(monthStart)}::date
        AND event_day < ${utcDate(tomorrow)}::date
    `;

    const row = rows[0];
    return {
      eventsToday: row ? asCount(row.events_today) : 0,
      dailyActiveInstallations: row ? asCount(row.daily_active_installations) : 0,
      weeklyActiveInstallations: row ? asCount(row.weekly_active_installations) : 0,
      monthlyActiveInstallations: row ? asCount(row.monthly_active_installations) : 0,
      lastEventAt: row?.last_event_at ? new Date(row.last_event_at) : null,
    };
  }

  async getMonthlyReport(year: number, month: number): Promise<MonthlyReportRow[]> {
    if (!Number.isInteger(year) || year < 1 || year > 9999 || !Number.isInteger(month) || month < 1 || month > 12) {
      throw new RangeError("year and month must identify a valid calendar month");
    }

    const start = new Date(Date.UTC(year, month - 1, 1));
    const end = new Date(Date.UTC(year, month, 1));
    const rows = await this.sql<MonthlyRow[]>`
      SELECT
        event_day::text AS day,
        COUNT(*)::int AS event_count,
        COUNT(DISTINCT installation_id)::int AS active_installations
      FROM analytics_events
      WHERE event_day >= ${utcDate(start)}::date
        AND event_day < ${utcDate(end)}::date
      GROUP BY event_day
      ORDER BY event_day ASC
    `;

    return rows.map((row: MonthlyRow) => ({
      day: row.day,
      eventCount: asCount(row.event_count),
      activeInstallations: asCount(row.active_installations),
    }));
  }

  async getMonthlyAggregates(startDate: string, endDate: string): Promise<MonthlyAggregateRow[]> {
    const rows = await this.sql<MonthlyAggregateSqlRow[]>`
      SELECT
        to_char(date_trunc('month', event_day), 'YYYY-MM') AS month,
        COUNT(*)::int AS event_count,
        COUNT(DISTINCT installation_id)::int AS active_installations
      FROM analytics_events
      WHERE event_day >= ${startDate}::date
        AND event_day <= ${endDate}::date
      GROUP BY date_trunc('month', event_day)
      ORDER BY date_trunc('month', event_day) ASC
    `;

    return rows.map((row) => ({
      month: row.month,
      eventCount: asCount(row.event_count),
      activeInstallations: asCount(row.active_installations),
    }));
  }

  async getBreakdowns(startDate: string, endDate: string): Promise<AnalyticsBreakdown> {
    const rows = await this.sql<BreakdownSqlRow[]>`
      SELECT 'version' AS dimension, app_version AS key, COUNT(*)::int AS count
      FROM analytics_events
      WHERE event_day >= ${startDate}::date AND event_day <= ${endDate}::date
      GROUP BY app_version
      UNION ALL
      SELECT 'platform' AS dimension, platform AS key, COUNT(*)::int AS count
      FROM analytics_events
      WHERE event_day >= ${startDate}::date AND event_day <= ${endDate}::date
      GROUP BY platform
      UNION ALL
      SELECT 'country' AS dimension, country_code AS key, COUNT(*)::int AS count
      FROM analytics_events
      WHERE event_day >= ${startDate}::date AND event_day <= ${endDate}::date
        AND country_code IS NOT NULL
      GROUP BY country_code
      ORDER BY dimension ASC, count DESC, key ASC
    `;

    const result: AnalyticsBreakdown = { version: [], platform: [], country: [] };
    for (const row of rows) {
      if (!row.key) continue;
      const item = { key: row.key, count: asCount(row.count) };
      if (row.dimension === "version") result.version.push(item);
      else if (row.dimension === "platform") result.platform.push(item);
      else if (row.dimension === "country") result.country.push(item);
    }
    return result;
  }
}
