import assert from "node:assert/strict";
import test from "node:test";
import { AnalyticsRepository } from "../lib/analytics-repository.ts";

type EventRow = {
  installationId: string;
  eventName: string;
  occurredAt: Date;
  eventDay: string;
};

class SqlHarness {
  readonly queries: string[] = [];
  readonly events: EventRow[] = [];

  async query(strings: TemplateStringsArray, ...values: readonly unknown[]): Promise<readonly object[]> {
    const query = strings.join("?");
    this.queries.push(query);

    if (query.includes("INSERT INTO analytics_events") && query.includes("WHERE NOT EXISTS")) {
      const [installationId, eventName, , , , occurredAt, eventDay] = values as string[];
      const duplicate = this.events.some(
        (event) => event.installationId === installationId && event.eventName === eventName && event.eventDay === eventDay,
      );
      if (duplicate) return [];
      this.events.push({ installationId, eventName, occurredAt: new Date(occurredAt), eventDay });
      return [{ id: "1" }];
    }

    if (query.includes("INSERT INTO analytics_events")) {
      const [installationId, eventName, , , , occurredAt, eventDay] = values as string[];
      this.events.push({ installationId, eventName, occurredAt: new Date(occurredAt), eventDay });
      return [];
    }

    if (query.includes("COUNT(*) FILTER")) {
      const [today, , sevenDayStart, , monthStart, tomorrow] = values as string[];
      const inRange = (event: EventRow, start: string, end: string) => event.eventDay >= start && event.eventDay < end;
      const todayEvents = this.events.filter((event) => event.eventDay === today);
      const distinct = (events: EventRow[]) => new Set(events.map((event) => event.installationId)).size;
      return [{
        events_today: todayEvents.length,
        daily_active_installations: distinct(todayEvents),
        weekly_active_installations: distinct(this.events.filter((event) => inRange(event, sevenDayStart, tomorrow))),
        monthly_active_installations: distinct(this.events.filter((event) => inRange(event, monthStart, tomorrow))),
        last_event_at: this.events
          .filter((event) => inRange(event, monthStart, tomorrow))
          .sort((left, right) => right.occurredAt.getTime() - left.occurredAt.getTime())[0]?.occurredAt ?? null,
      }];
    }

    if (query.includes("GROUP BY event_day")) {
      const [start, end] = values as string[];
      const rows = new Map<string, EventRow[]>();
      for (const event of this.events.filter((item) => item.eventDay >= start && item.eventDay < end)) {
        rows.set(event.eventDay, [...(rows.get(event.eventDay) ?? []), event]);
      }
      return [...rows.entries()].sort(([left], [right]) => left.localeCompare(right)).map(([day, events]) => ({
        day,
        event_count: events.length,
        active_installations: new Set(events.map((event) => event.installationId)).size,
      }));
    }

    throw new Error(`Unexpected SQL: ${query}`);
  }
}

const installationA = "11111111-1111-4111-8111-111111111111";
const installationB = "22222222-2222-4222-8222-222222222222";
const at = new Date("2026-08-09T12:00:00.000Z");

function repository(harness: SqlHarness): AnalyticsRepository {
  return new AnalyticsRepository(harness.query.bind(harness) as never);
}

test("counts distinct installations for DAU and ignores daily idempotent duplicates", async () => {
  const harness = new SqlHarness();
  const repo = repository(harness);

  await repo.insertEvent({ installationId: installationA, eventName: "app_opened", appVersion: "4.0.0", platform: "Windows", occurredAt: new Date("2026-08-09T01:00:00Z"), dailyIdempotent: true });
  await repo.insertEvent({ installationId: installationA, eventName: "app_opened", appVersion: "4.0.0", platform: "Windows", occurredAt: new Date("2026-08-09T02:00:00Z"), dailyIdempotent: true });
  await repo.insertEvent({ installationId: installationB, eventName: "app_opened", appVersion: "4.0.0", platform: "Linux", occurredAt: new Date("2026-08-09T03:00:00Z"), dailyIdempotent: true });
  await repo.insertEvent({ installationId: installationA, eventName: "session_started", appVersion: "4.0.0", platform: "Windows", occurredAt: new Date("2026-08-09T04:00:00Z") });
  await repo.insertEvent({ installationId: installationA, eventName: "session_started", appVersion: "4.0.0", platform: "Windows", occurredAt: new Date("2026-08-09T05:00:00Z") });

  const overview = await repo.getOverview(at);
  assert.equal(harness.events.length, 4);
  assert.equal(overview.eventsToday, 4);
  assert.equal(overview.dailyActiveInstallations, 2);
  assert.equal(overview.weeklyActiveInstallations, 2);
});

test("uses seven-day and calendar-month UTC boundaries", async () => {
  const harness = new SqlHarness();
  const repo = repository(harness);
  const events = [
    [installationA, "old", "2026-08-02T23:59:59Z"],
    [installationB, "boundary", "2026-08-03T00:00:00Z"],
    [installationA, "today", "2026-08-09T23:59:59Z"],
    [installationB, "next-month", "2026-09-01T00:00:00Z"],
  ] as const;
  for (const [installationId, eventName, occurredAt] of events) {
    await repo.insertEvent({ installationId, eventName, appVersion: "4.0.0", platform: "Windows", occurredAt: new Date(occurredAt) });
  }

  const overview = await repo.getOverview(at);
  assert.equal(overview.dailyActiveInstallations, 1);
  assert.equal(overview.weeklyActiveInstallations, 2);
  assert.equal(overview.monthlyActiveInstallations, 2);

  const monthly = await repo.getMonthlyReport(2026, 8);
  assert.deepEqual(monthly, [
    { day: "2026-08-02", eventCount: 1, activeInstallations: 1 },
    { day: "2026-08-03", eventCount: 1, activeInstallations: 1 },
    { day: "2026-08-09", eventCount: 1, activeInstallations: 1 },
  ]);
});

test("returns zero overview and empty monthly report for empty data", async () => {
  const repo = repository(new SqlHarness());
  assert.deepEqual(await repo.getOverview(at), {
    eventsToday: 0,
    dailyActiveInstallations: 0,
    weeklyActiveInstallations: 0,
    monthlyActiveInstallations: 0,
    lastEventAt: null,
  });
  assert.deepEqual(await repo.getMonthlyReport(2026, 8), []);
});

test("validates event input at the repository boundary", async () => {
  const repo = repository(new SqlHarness());
  await assert.rejects(
    repo.insertEvent({ installationId: "not-a-uuid", eventName: "app_opened", appVersion: "4.0.0", platform: "Windows", occurredAt: at }),
    /installationId must be a UUID/,
  );
  await assert.rejects(
    repo.insertEvent({ installationId: installationA, eventName: "app_opened", appVersion: "4.0.0", platform: "Windows", occurredAt: at, countryCode: "USA" }),
    /countryCode must contain two letters/,
  );
});
