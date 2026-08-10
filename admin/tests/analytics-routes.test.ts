import assert from "node:assert/strict";
import test from "node:test";
import { handleGet as handleOverviewGet } from "../app/api/analytics/overview/route.ts";
import { handleGet as handleMonthlyGet } from "../app/api/analytics/monthly/route.ts";

type Reader = {
  getOverview(at: Date): Promise<{
    eventsToday: number;
    dailyActiveInstallations: number;
    weeklyActiveInstallations: number;
    monthlyActiveInstallations: number;
    lastEventAt: Date | null;
  }>;
  getMonthlyReport(year: number, month: number): Promise<Array<{
    day: string;
    eventCount: number;
    activeInstallations: number;
  }>>;
  getMonthlyAggregates?(startDate: string, endDate: string): Promise<Array<{
    month: string;
    eventCount: number;
    activeInstallations: number;
  }>>;
  getBreakdowns?(startDate: string, endDate: string): Promise<{
    version: Array<{ key: string; count: number }>;
    platform: Array<{ key: string; count: number }>;
    country: Array<{ key: string; count: number }>;
  }>;
};

const reportRows = [
  { day: "2026-08-08", eventCount: 2, activeInstallations: 1 },
  { day: "2026-08-09", eventCount: 3, activeInstallations: 2 },
];

function request(path: string): Request {
  return new Request(`http://localhost${path}`);
}

function reader(overrides: Partial<Reader> = {}): Reader {
  return {
    async getOverview() {
      return {
        eventsToday: 3,
        dailyActiveInstallations: 2,
        weeklyActiveInstallations: 4,
        monthlyActiveInstallations: 8,
        lastEventAt: new Date("2026-08-09T12:00:00.000Z"),
      };
    },
    async getMonthlyReport(year, month) {
      return year === 2026 && month === 8 ? reportRows : [];
    },
    ...overrides,
  };
}

test("overview defaults to a recent 30-day UTC range", async () => {
  const calls: Date[] = [];
  const response = await handleOverviewGet(request("/api/analytics/overview"), reader({
    async getOverview(at) {
      calls.push(at);
      return reader().getOverview(at);
    },
  }));

  assert.equal(response.status, 200);
  const body = await response.json();
  assert.equal(body.ok, true);
  assert.match(body.data.start_date, /^\d{4}-\d{2}-\d{2}$/);
  assert.match(body.data.end_date, /^\d{4}-\d{2}-\d{2}$/);
  assert.equal(calls.length, 1);
  assert.equal(body.data.daily_series.length, 2);
  assert.deepEqual(body.data.version_breakdown, []);
});

test("uses range-wide distinct monthly aggregates and real breakdown rows", async () => {
  const response = await handleOverviewGet(
    request("/api/analytics/overview?start_date=2026-08-01&end_date=2026-08-31"),
    reader({
      async getMonthlyAggregates() {
        return [{ month: "2026-08", eventCount: 5, activeInstallations: 3 }];
      },
      async getBreakdowns() {
        return {
          version: [{ key: "4.0.0", count: 5 }],
          platform: [{ key: "windows", count: 5 }],
          country: [{ key: "JP", count: 3 }],
        };
      },
    }),
  );

  const body = await response.json();
  assert.deepEqual(body.data.monthly_series, [{ month: "2026-08", event_count: 5, active_installations: 3 }]);
  assert.deepEqual(body.data.version_breakdown, [{ key: "4.0.0", count: 5 }]);
  assert.deepEqual(body.data.platform_breakdown, [{ key: "windows", count: 5 }]);
  assert.deepEqual(body.data.country_breakdown, [{ key: "JP", count: 3 }]);
});

test("monthly accepts an explicit UTC range and returns typed series", async () => {
  const calls: Array<[number, number]> = [];
  const response = await handleMonthlyGet(
    request("/api/analytics/monthly?start_date=2026-08-01&end_date=2026-08-09"),
    reader({
      async getMonthlyReport(year, month) {
        calls.push([year, month]);
        return reportRows;
      },
    }),
  );

  assert.equal(response.status, 200);
  const body = await response.json();
  assert.equal(body.ok, true);
  assert.deepEqual(calls, [[2026, 8]]);
  assert.deepEqual(body.data.monthly_series, [
    { month: "2026-08", event_count: 5, active_installations: 2 },
  ]);
});

test("empty repository data produces empty series and zero KPIs", async () => {
  const empty = reader({
    async getOverview() {
      return {
        eventsToday: 0,
        dailyActiveInstallations: 0,
        weeklyActiveInstallations: 0,
        monthlyActiveInstallations: 0,
        lastEventAt: null,
      };
    },
    async getMonthlyReport() {
      return [];
    },
  });
  const response = await handleOverviewGet(request("/api/analytics/overview?start=2026-08-01&end=2026-08-09"), empty);
  assert.equal(response.status, 200);
  const body = await response.json();
  assert.deepEqual(body.data.daily_series, []);
  assert.deepEqual(body.data.monthly_series, []);
  assert.equal(body.data.kpis.events_today, 0);
});

test("rejects invalid and overlong UTC ranges", async () => {
  for (const query of [
    "?start_date=2026-08-10&end_date=2026-08-09",
    "?start_date=2026-02-30&end_date=2026-03-01",
    "?start_date=2025-01-01&end_date=2026-08-09",
  ]) {
    const response = await handleOverviewGet(request(`/api/analytics/overview${query}`), reader());
    assert.equal(response.status, 400);
    assert.equal((await response.json()).error.code, "INVALID_RANGE");
  }
});

test("maps repository failures to a safe error envelope", async () => {
  const response = await handleMonthlyGet(request("/api/analytics/monthly?start=2026-08-01&end=2026-08-09"), reader({
    async getMonthlyReport() {
      throw new Error("SQL password must not escape");
    },
  }));

  assert.equal(response.status, 500);
  assert.deepEqual(await response.json(), {
    ok: false,
    error: { code: "INTERNAL_ERROR", message: "Unable to load analytics." },
  });
});
