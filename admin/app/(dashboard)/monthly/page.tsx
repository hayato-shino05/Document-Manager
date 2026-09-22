"use client";

import { Suspense, useCallback, useEffect, useMemo, useState } from "react";
import { usePathname, useRouter, useSearchParams } from "next/navigation";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { MonthlyReportTable } from "@/components/analytics/monthly-report-table";
import { DashboardError, DashboardLoading } from "@/components/analytics/dashboard-state";
import { buildMonthlyRows, parseMonthlyReport, type MonthlyReportData } from "@/lib/monthly-report";

const MONTH_PATTERN = /^\d{4}-(0[1-9]|1[0-2])$/;

function currentUtcMonth(): string {
  const now = new Date();
  return `${now.getUTCFullYear()}-${String(now.getUTCMonth() + 1).padStart(2, "0")}`;
}

function monthEnd(month: string): string {
  const [year, monthNumber] = month.split("-").map(Number);
  return new Date(Date.UTC(year, monthNumber, 0)).toISOString().slice(0, 10);
}

function readMonth(value: string | null, fallback: string): string {
  return value && MONTH_PATTERN.test(value) ? value : fallback;
}

function MonthlyReportContent() {
  const searchParams = useSearchParams();
  const pathname = usePathname();
  const router = useRouter();
  const defaultMonth = currentUtcMonth();
  const [startMonth, setStartMonth] = useState(() => readMonth(searchParams.get("start_month"), defaultMonth));
  const [endMonth, setEndMonth] = useState(() => readMonth(searchParams.get("end_month"), defaultMonth));
  const [data, setData] = useState<MonthlyReportData | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const rangeIsValid = startMonth <= endMonth && (Number(endMonth.slice(0, 4)) - Number(startMonth.slice(0, 4))) * 12 + Number(endMonth.slice(5)) - Number(startMonth.slice(5)) <= 11;

  const updateUrl = useCallback((nextStart: string, nextEnd: string) => {
    const params = new URLSearchParams(searchParams.toString());
    params.set("start_month", nextStart);
    params.set("end_month", nextEnd);
    router.replace(`${pathname}?${params.toString()}`, { scroll: false });
  }, [pathname, router, searchParams]);

  const load = useCallback(async () => {
    if (!rangeIsValid) {
      setError("Choose a valid UTC month range.");
      setLoading(false);
      return;
    }
    setLoading(true);
    setError(null);
    try {
      const response = await fetch(`/api/analytics/monthly?start_date=${encodeURIComponent(`${startMonth}-01`)}&end_date=${encodeURIComponent(monthEnd(endMonth))}`, { cache: "no-store" });
      const body: unknown = await response.json();
      if (!response.ok) throw new Error("Unable to load analytics.");
      setData(parseMonthlyReport(body));
    } catch {
      setData(null);
      setError("Unable to load analytics. Check the local analytics database, then retry this request.");
    } finally {
      setLoading(false);
    }
  }, [endMonth, rangeIsValid, startMonth]);

  useEffect(() => {
    const timer = window.setTimeout(() => void load(), 0);
    return () => window.clearTimeout(timer);
  }, [load]);

  const rows = useMemo(() => data ? buildMonthlyRows(data, startMonth, endMonth) : [], [data, endMonth, startMonth]);
  function applyRange(nextStart: string, nextEnd: string) {
    setStartMonth(nextStart);
    setEndMonth(nextEnd);
    updateUrl(nextStart, nextEnd);
  }

  return (
    <main className="mx-auto max-w-7xl px-6 py-8">
      <div className="space-y-8">
        <div className="flex flex-wrap items-end justify-between gap-4">
          <div><p className="text-sm font-medium text-teal-800">Monthly report</p><h1 className="mt-1 text-3xl font-semibold tracking-tight text-stone-900">Calendar-month activity</h1><p className="mt-2 max-w-2xl text-sm text-stone-600">Review real event activity received from connected Study Document Manager installations.</p></div>
          <Button type="button" variant="secondary" onClick={() => void load()} disabled={loading}>Refresh</Button>
        </div>

        <Card>
          <CardHeader><CardTitle>UTC month range</CardTitle><CardDescription>Choose up to one year of calendar months. The selection is preserved in the URL.</CardDescription></CardHeader>
          <CardContent><div className="flex flex-wrap items-end gap-4"><label className="grid gap-1 text-sm font-medium text-stone-700">From (UTC)<input type="month" value={startMonth} onChange={(event) => applyRange(event.target.value, endMonth)} className="min-h-11 rounded-md border border-stone-300 bg-white px-3 font-normal text-stone-900 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-teal-700/50" /></label><label className="grid gap-1 text-sm font-medium text-stone-700">To (UTC)<input type="month" value={endMonth} onChange={(event) => applyRange(startMonth, event.target.value)} className="min-h-11 rounded-md border border-stone-300 bg-white px-3 font-normal text-stone-900 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-teal-700/50" /></label></div></CardContent>
        </Card>

        {loading ? <DashboardLoading /> : error ? <DashboardError onRetry={() => void load()} /> : <MonthlyReportTable rows={rows} />}
      </div>
    </main>
  );
}


export default function MonthlyPage() {
  return <Suspense fallback={<main className="mx-auto max-w-7xl px-6 py-8"><DashboardLoading /></main>}><MonthlyReportContent /></Suspense>;
}
