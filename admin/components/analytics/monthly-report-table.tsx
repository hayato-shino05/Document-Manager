"use client";

import { useMemo, useState } from "react";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { formatChange, formatMonth, type MonthlyReportRow } from "@/lib/monthly-report";

type SortKey = "month" | "activeInstallations" | "sessions" | "eventCount" | "eventChange";
type SortDirection = "ascending" | "descending" | "none";

type MonthlyReportTableProps = {
  rows: MonthlyReportRow[];
};

function compare(left: MonthlyReportRow, right: MonthlyReportRow, key: SortKey): number {
  if (key === "month") return left.month.localeCompare(right.month);
  const leftValue = left[key] ?? Number.NEGATIVE_INFINITY;
  const rightValue = right[key] ?? Number.NEGATIVE_INFINITY;
  return Number(leftValue) - Number(rightValue);
}

export function MonthlyReportTable({ rows }: MonthlyReportTableProps) {
  const [sortKey, setSortKey] = useState<SortKey>("month");
  const [ascending, setAscending] = useState(false);
  const sortedRows = useMemo(() => [...rows].sort((left, right) => (ascending ? 1 : -1) * compare(left, right, sortKey)), [ascending, rows, sortKey]);

  function sortBy(nextKey: SortKey) {
    if (nextKey === sortKey) setAscending((value) => !value);
    else {
      setSortKey(nextKey);
      setAscending(true);
    }
  }

  function sortLabel(key: SortKey): SortDirection {
    return sortKey === key ? (ascending ? "ascending" : "descending") : "none";
  }

  function heading(label: string, key: SortKey, className = "") {
    return <TableHead scope="col" aria-sort={sortLabel(key)} className={className}><button type="button" className="min-h-11 text-left font-medium underline-offset-4 hover:underline focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-teal-700/50" onClick={() => sortBy(key)}>{label}</button></TableHead>;
  }

  const hasData = rows.some((row) => row.eventCount > 0 || row.activeInstallations > 0 || row.sessions !== null);

  return (
    <Card>
      <CardHeader><CardTitle>Monthly activity</CardTitle><CardDescription>UTC calendar months. Event change compares with the preceding displayed month; sessions are only shown when the API reports them.</CardDescription></CardHeader>
      <CardContent>
        {!hasData ? <p className="py-6 text-sm text-stone-500">No analytics events were reported for this range.</p> : (
          <Table>
            <TableHeader><TableRow>{heading("Month", "month")}{heading("Active installations", "activeInstallations", "text-right")}{heading("Sessions", "sessions", "text-right")}{heading("Events", "eventCount", "text-right")}{heading("Event change", "eventChange", "text-right")}</TableRow></TableHeader>
            <TableBody>{sortedRows.map((row) => <TableRow key={row.month}><TableCell className="whitespace-nowrap font-medium">{formatMonth(row.month)}</TableCell><TableCell className="text-right tabular-nums">{row.activeInstallations.toLocaleString()}</TableCell><TableCell className="text-right tabular-nums">{row.sessions === null ? "Not reported" : row.sessions.toLocaleString()}</TableCell><TableCell className="text-right tabular-nums">{row.eventCount.toLocaleString()}</TableCell><TableCell className="text-right tabular-nums">{formatChange(row.eventChange)}</TableCell></TableRow>)}</TableBody>
          </Table>
        )}
      </CardContent>
    </Card>
  );
}
