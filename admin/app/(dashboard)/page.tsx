"use client";

import { useCallback, useEffect, useMemo, useState } from "react";
import type { AnalyticsResponseData } from "@/lib/analytics-query";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { KpiCard } from "@/components/analytics/kpi-card";
import { BreakdownTable } from "@/components/analytics/breakdown-table";
import { DashboardEmpty, DashboardError, DashboardLoading } from "@/components/analytics/dashboard-state";
import { TrendChart } from "@/components/analytics/trend-chart";

function isAnalyticsResponseData(value: unknown): value is AnalyticsResponseData {
  if (!value || typeof value !== "object") return false;
  const data = value as Record<string, unknown>;
  const kpis = data.kpis;
  if (!kpis || typeof kpis !== "object") return false;
  const measures = kpis as Record<string, unknown>;
  const isBreakdown = (row: unknown): row is { key: string; count: number } => Boolean(row && typeof row === "object" && typeof (row as Record<string, unknown>).key === "string" && typeof (row as Record<string, unknown>).count === "number");
  const isDaily = (row: unknown): boolean => Boolean(row && typeof row === "object" && typeof (row as Record<string, unknown>).day === "string" && typeof (row as Record<string, unknown>).event_count === "number" && typeof (row as Record<string, unknown>).active_installations === "number");
  const isMonthly = (row: unknown): boolean => Boolean(row && typeof row === "object" && typeof (row as Record<string, unknown>).month === "string" && typeof (row as Record<string, unknown>).event_count === "number" && typeof (row as Record<string, unknown>).active_installations === "number");
  return typeof data.start_date === "string" && typeof data.end_date === "string" && typeof data.generated_at === "string" &&
    typeof measures.events_today === "number" && typeof measures.daily_active_installations === "number" && typeof measures.weekly_active_installations === "number" && typeof measures.monthly_active_installations === "number" &&
    (measures.last_event_at === null || typeof measures.last_event_at === "string") && Array.isArray(data.daily_series) && data.daily_series.every(isDaily) && Array.isArray(data.monthly_series) && data.monthly_series.every(isMonthly) &&
    Array.isArray(data.version_breakdown) && data.version_breakdown.every(isBreakdown) && Array.isArray(data.platform_breakdown) && data.platform_breakdown.every(isBreakdown) && Array.isArray(data.country_breakdown) && data.country_breakdown.every(isBreakdown);
}

async function readAnalytics(): Promise<AnalyticsResponseData> {
  const response = await fetch("/api/analytics/overview", { cache: "no-store" });
  const body: unknown = await response.json();
  if (!response.ok || !body || typeof body !== "object" || !("ok" in body) || body.ok !== true || !("data" in body) || !isAnalyticsResponseData(body.data)) {
    throw new Error("Analytics request failed");
  }
  return body.data;
}

function formatTimestamp(value: string | null): string {
  if (!value) return "No event timestamp reported";
  return new Intl.DateTimeFormat(undefined, { dateStyle: "medium", timeStyle: "short" }).format(new Date(value));
}

export default function OverviewPage() {
  const [data, setData] = useState<AnalyticsResponseData | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(false);

  const load = useCallback(async () => {
    setLoading(true);
    setError(false);
    try {
      setData(await readAnalytics());
    } catch {
      setError(true);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    const timer = window.setTimeout(() => void load(), 0);
    return () => window.clearTimeout(timer);
  }, [load]);

  const dailyPoints = useMemo(() => data?.daily_series.map((row) => ({ label: row.day, value: row.event_count })) ?? [], [data]);
  const monthlyPoints = useMemo(() => data?.monthly_series.map((row) => ({ label: row.month, value: row.event_count })) ?? [], [data]);
  const hasEvents = Boolean(data && (data.daily_series.length > 0 || data.kpis.events_today > 0));

  return (
    <main className="mx-auto max-w-7xl px-6 py-8">
      <div className="space-y-8">
        <div className="flex flex-wrap items-end justify-between gap-4">
          <div>
            <p className="text-sm font-medium text-teal-800">Overview</p>
            <h1 className="mt-1 text-3xl font-semibold tracking-tight text-stone-900">Analytics at a glance</h1>
            <p className="mt-2 max-w-2xl text-sm text-stone-600">A local-only view of activity received from connected Study Document Manager installations.</p>
          </div>
          <Button type="button" variant="secondary" onClick={() => void load()} disabled={loading}>Refresh</Button>
        </div>

        {loading ? <DashboardLoading /> : error ? <DashboardError onRetry={() => void load()} /> : data && !hasEvents ? <DashboardEmpty /> : null}

        {!loading && !error && data ? (
          <>
            <section aria-labelledby="kpi-heading">
              <div className="mb-3 flex items-baseline justify-between gap-4"><h2 id="kpi-heading" className="text-lg font-semibold text-stone-900">Key measures</h2><p className="text-xs text-stone-500">Range: {data.start_date} to {data.end_date}</p></div>
              <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-5">
                <KpiCard label="DAU" value={data.kpis.daily_active_installations} description="Unique installations active today." />
                <KpiCard label="WAU" value={data.kpis.weekly_active_installations} description="Unique installations active in seven days." />
                <KpiCard label="MAU" value={data.kpis.monthly_active_installations} description="Unique installations active this month." />
                <KpiCard label="Active installations" value={null} description="Installation inventory is not exposed by the current API." />
                <KpiCard label="Current month sessions" value={null} description="Session totals are not exposed by the current API." />
              </div>
            </section>

            <section aria-labelledby="trend-heading" className="space-y-6">
              <div><h2 id="trend-heading" className="text-lg font-semibold text-stone-900">Activity trends</h2><p className="mt-1 text-sm text-stone-600">Events reported by day and month. Values are sourced directly from the analytics API.</p></div>
              <TrendChart title="Daily activity" description="Event count by UTC day" points={dailyPoints} valueLabel="Events" />
              <div id="monthly-report"><TrendChart title="Monthly trend" description="Event count by UTC month" points={monthlyPoints} valueLabel="Events" /></div>
            </section>

            <section aria-labelledby="breakdown-heading" className="space-y-4">
              <div><h2 id="breakdown-heading" className="text-lg font-semibold text-stone-900">Breakdowns</h2><p className="mt-1 text-sm text-stone-600">These dimensions appear when the API provides breakdown rows.</p></div>
              <div className="grid gap-6 xl:grid-cols-3">
                <BreakdownTable title="App version" description="Events by reported version" rows={data.version_breakdown} />
                <BreakdownTable title="Platform" description="Events by reported platform" rows={data.platform_breakdown} />
                <BreakdownTable title="Country" description="Events by reported country" rows={data.country_breakdown} />
              </div>
            </section>

            <Card>
              <CardHeader><CardTitle>Data freshness</CardTitle><CardDescription>Use this timestamp to confirm the latest event visible to the dashboard.</CardDescription></CardHeader>
              <CardContent className="flex flex-wrap items-center justify-between gap-3"><p className="text-sm text-stone-700"><span className="font-medium">Last event:</span> {formatTimestamp(data.kpis.last_event_at)}</p><p className="text-sm text-stone-500">Dashboard generated {formatTimestamp(data.generated_at)}</p></CardContent>
            </Card>
          </>
        ) : null}
      </div>
    </main>
  );
}
