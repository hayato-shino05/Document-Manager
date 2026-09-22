export type MonthlyApiRow = {
  month: string;
  active_installations: number;
  event_count: number;
  sessions?: number;
};

export type MonthlyReportRow = {
  month: string;
  activeInstallations: number;
  sessions: number | null;
  eventCount: number;
  eventChange: number | null;
};

export type MonthlyReportData = {
  start_date: string;
  end_date: string;
  monthly_series: MonthlyApiRow[];
};

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}

function isMonthlyApiRow(value: unknown): value is MonthlyApiRow {
  if (!isRecord(value) || typeof value.month !== "string" || typeof value.active_installations !== "number" || typeof value.event_count !== "number") return false;
  return value.sessions === undefined || typeof value.sessions === "number";
}

export function parseMonthlyReport(value: unknown): MonthlyReportData {
  if (!isRecord(value) || value.ok !== true || !isRecord(value.data)) throw new Error("Analytics request failed");
  const data = value.data;
  if (typeof data.start_date !== "string" || typeof data.end_date !== "string" || !Array.isArray(data.monthly_series) || !data.monthly_series.every(isMonthlyApiRow)) {
    throw new Error("Analytics response is invalid");
  }
  return { start_date: data.start_date, end_date: data.end_date, monthly_series: data.monthly_series };
}

function monthKey(year: number, month: number): string {
  return `${year.toString().padStart(4, "0")}-${month.toString().padStart(2, "0")}`;
}

export function buildMonthlyRows(data: MonthlyReportData, startMonth: string, endMonth: string): MonthlyReportRow[] {
  const values = new Map(data.monthly_series.map((row) => [row.month, row]));
  const [startYear, startMonthNumber] = startMonth.split("-").map(Number);
  const [endYear, endMonthNumber] = endMonth.split("-").map(Number);
  const rows: MonthlyReportRow[] = [];
  let year = startYear;
  let month = startMonthNumber;
  let previousEventCount: number | null = null;

  while (year < endYear || (year === endYear && month <= endMonthNumber)) {
    const key = monthKey(year, month);
    const value = values.get(key);
    const eventCount = value?.event_count ?? 0;
    rows.push({
      month: key,
      activeInstallations: value?.active_installations ?? 0,
      sessions: value?.sessions ?? null,
      eventCount,
      eventChange: previousEventCount === null ? null : eventCount - previousEventCount,
    });
    previousEventCount = eventCount;
    month += 1;
    if (month === 13) {
      month = 1;
      year += 1;
    }
  }
  return rows;
}

export function formatMonth(month: string): string {
  const [year, monthNumber] = month.split("-").map(Number);
  return new Intl.DateTimeFormat(undefined, { month: "long", year: "numeric", timeZone: "UTC" }).format(new Date(Date.UTC(year, monthNumber - 1, 1)));
}

export function formatChange(change: number | null): string {
  if (change === null) return "—";
  return `${change > 0 ? "+" : ""}${change.toLocaleString()}`;
}
