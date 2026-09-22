import assert from "node:assert/strict";
import test from "node:test";
import { buildMonthlyRows, formatChange, formatMonth, parseMonthlyReport } from "../lib/monthly-report.ts";

test("builds one row per UTC calendar month and compares event counts", () => {
  const rows = buildMonthlyRows({ start_date: "2026-01-01", end_date: "2026-03-31", monthly_series: [
    { month: "2026-01", active_installations: 2, event_count: 4 },
    { month: "2026-03", active_installations: 3, event_count: 9 },
  ] }, "2026-01", "2026-03");

  assert.deepEqual(rows.map((row) => ({ month: row.month, events: row.eventCount, change: row.eventChange })), [
    { month: "2026-01", events: 4, change: null },
    { month: "2026-02", events: 0, change: -4 },
    { month: "2026-03", events: 9, change: 9 },
  ]);
});

test("formats month and change values for display", () => {
  assert.match(formatMonth("2026-03"), /2026/);
  assert.equal(formatChange(9), "+9");
  assert.equal(formatChange(null), "—");
});

test("rejects malformed API responses instead of rendering fake metrics", () => {
  assert.throws(() => parseMonthlyReport({ ok: true, data: { monthly_series: [{ month: "bad" }] } }));
});
