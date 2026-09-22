import type { AnalyticsBreakdown, AnalyticsOverview, MonthlyAggregateRow, MonthlyReportRow } from "./analytics-repository.ts";

const DAY_MS = 24 * 60 * 60 * 1000;
const DEFAULT_RANGE_DAYS = 30;
const MAX_RANGE_DAYS = 366;
const DATE_PATTERN = /^(\d{4})-(\d{2})-(\d{2})$/;

export type AnalyticsReader = {
  getOverview(at: Date): Promise<AnalyticsOverview>;
  getMonthlyReport(year: number, month: number): Promise<MonthlyReportRow[]>;
  getMonthlyAggregates?(startDate: string, endDate: string): Promise<MonthlyAggregateRow[]>;
  getBreakdowns?(startDate: string, endDate: string): Promise<AnalyticsBreakdown>;
};

type DateRange = {
  start: Date;
  end: Date;
  startDate: string;
  endDate: string;
};

type Breakdown = { key: string; count: number };
type MonthlySeriesRow = { month: string; event_count: number; active_installations: number };

export type AnalyticsResponseData = {
  start_date: string;
  end_date: string;
  kpis: {
    events_today: number;
    daily_active_installations: number;
    weekly_active_installations: number;
    monthly_active_installations: number;
    last_event_at: string | null;
  };
  daily_series: Array<{
    day: string;
    event_count: number;
    active_installations: number;
  }>;
  monthly_series: Array<{
    month: string;
    event_count: number;
    active_installations: number;
  }>;
  version_breakdown: Breakdown[];
  platform_breakdown: Breakdown[];
  country_breakdown: Breakdown[];
  generated_at: string;
};

export class AnalyticsRangeError extends Error {
  constructor(message: string) {
    super(message);
    this.name = "AnalyticsRangeError";
  }
}

function utcDate(value: Date): string {
  return value.toISOString().slice(0, 10);
}

function parseDate(value: string, field: string): Date {
  const match = DATE_PATTERN.exec(value);
  if (!match) throw new AnalyticsRangeError(`${field} must be an ISO UTC date.`);
  const date = new Date(Date.UTC(Number(match[1]), Number(match[2]) - 1, Number(match[3])));
  if (date.getUTCFullYear() !== Number(match[1]) || date.getUTCMonth() !== Number(match[2]) - 1 || date.getUTCDate() !== Number(match[3])) {
    throw new AnalyticsRangeError(`${field} must be a valid UTC date.`);
  }
  return date;
}

function queryValue(url: URL, names: string[]): string | null {
  const values = names.map((name) => url.searchParams.get(name)).filter((value): value is string => value !== null);
  if (new Set(values).size > 1) throw new AnalyticsRangeError("Date range parameters must not conflict.");
  return values[0] ?? null;
}

export function parseDateRange(request: Request, now = new Date()): DateRange {
  const url = new URL(request.url);
  const startValue = queryValue(url, ["start_date", "start", "from"]);
  const endValue = queryValue(url, ["end_date", "end", "to"]);
  const today = new Date(Date.UTC(now.getUTCFullYear(), now.getUTCMonth(), now.getUTCDate()));
  const end = endValue ? parseDate(endValue, "end_date") : today;
  const start = startValue ? parseDate(startValue, "start_date") : new Date(end.getTime() - (DEFAULT_RANGE_DAYS - 1) * DAY_MS);
  const days = Math.floor((end.getTime() - start.getTime()) / DAY_MS) + 1;
  if (start > end || days < 1 || days > MAX_RANGE_DAYS) {
    throw new AnalyticsRangeError(`Date range must contain between 1 and ${MAX_RANGE_DAYS} UTC days.`);
  }
  return { start, end, startDate: utcDate(start), endDate: utcDate(end) };
}

function monthKeys(range: DateRange): Array<[number, number]> {
  const result: Array<[number, number]> = [];
  const cursor = new Date(Date.UTC(range.start.getUTCFullYear(), range.start.getUTCMonth(), 1));
  const last = new Date(Date.UTC(range.end.getUTCFullYear(), range.end.getUTCMonth(), 1));
  while (cursor <= last) {
    result.push([cursor.getUTCFullYear(), cursor.getUTCMonth() + 1]);
    cursor.setUTCMonth(cursor.getUTCMonth() + 1);
  }
  return result;
}

export async function loadAnalyticsData(reader: AnalyticsReader, range: DateRange): Promise<AnalyticsResponseData> {
  const [overview, monthlyRows, aggregateRows, breakdowns] = await Promise.all([
    reader.getOverview(range.end),
    Promise.all(monthKeys(range).map(([year, month]) => reader.getMonthlyReport(year, month))).then((parts) => parts.flat()),
    reader.getMonthlyAggregates?.(range.startDate, range.endDate),
    reader.getBreakdowns?.(range.startDate, range.endDate),
  ]);
  const dailyRows = monthlyRows.filter((row) => row.day >= range.startDate && row.day <= range.endDate);
  const monthly: MonthlySeriesRow[] = aggregateRows
    ? aggregateRows.map((row) => ({ month: row.month, event_count: row.eventCount, active_installations: row.activeInstallations }))
    : [...dailyRows.reduce((values, row) => {
      const month = row.day.slice(0, 7);
      const current = values.get(month) ?? { event_count: 0, active_installations: 0 };
      current.event_count += row.eventCount;
      current.active_installations = Math.max(current.active_installations, row.activeInstallations);
      values.set(month, current);
      return values;
    }, new Map<string, Omit<MonthlySeriesRow, "month">>()).entries()]
      .map(([month, values]) => ({ ...values, month }));
  return {
    start_date: range.startDate,
    end_date: range.endDate,
    kpis: {
      events_today: overview.eventsToday,
      daily_active_installations: overview.dailyActiveInstallations,
      weekly_active_installations: overview.weeklyActiveInstallations,
      monthly_active_installations: overview.monthlyActiveInstallations,
      last_event_at: overview.lastEventAt?.toISOString() ?? null,
    },
    daily_series: dailyRows.map((row) => ({ day: row.day, event_count: row.eventCount, active_installations: row.activeInstallations })),
    monthly_series: monthly.sort((left, right) => left.month.localeCompare(right.month)),
    version_breakdown: breakdowns?.version ?? [],
    platform_breakdown: breakdowns?.platform ?? [],
    country_breakdown: breakdowns?.country ?? [],
    generated_at: new Date().toISOString(),
  };
}
