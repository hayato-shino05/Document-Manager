import Link from "next/link";
import type { ReactNode } from "react";
import { Activity, CalendarDays } from "lucide-react";

export default function DashboardLayout({ children }: Readonly<{ children: ReactNode }>) {
  return (
    <div className="min-h-screen">
      <header className="border-b border-stone-200 bg-white">
        <div className="mx-auto flex max-w-7xl flex-wrap items-center justify-between gap-4 px-6 py-5">
          <div>
            <p className="text-xs font-semibold uppercase tracking-[0.18em] text-teal-800">Study Document Manager</p>
            <p className="mt-1 text-xl font-semibold tracking-tight text-stone-900">Operations analytics</p>
          </div>
          <nav aria-label="Analytics navigation" className="flex flex-wrap gap-1 text-sm">
            <Link href="/" className="inline-flex min-h-11 items-center gap-2 rounded-md px-3 font-medium text-stone-700 hover:bg-stone-100 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-teal-700/50">
              <Activity className="size-4" aria-hidden="true" />Overview
            </Link>
            <Link href="/monthly" className="inline-flex min-h-11 items-center gap-2 rounded-md px-3 font-medium text-stone-700 hover:bg-stone-100 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-teal-700/50">
              <CalendarDays className="size-4" aria-hidden="true" />Monthly report
            </Link>
          </nav>
        </div>
      </header>
      {children}
    </div>
  );
}
