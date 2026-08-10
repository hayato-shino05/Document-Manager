import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Skeleton } from "@/components/ui/skeleton";

type DashboardStateProps = { onRetry?: () => void };

export function DashboardLoading() {
  return (
    <div className="space-y-6" aria-busy="true" aria-label="Loading analytics">
      <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
        {Array.from({ length: 4 }, (_, index) => <Skeleton key={index} className="h-36" />)}
      </div>
      <Skeleton className="h-80" />
      <div className="grid gap-6 xl:grid-cols-3">
        {Array.from({ length: 3 }, (_, index) => <Skeleton key={index} className="h-64" />)}
      </div>
    </div>
  );
}

export function DashboardEmpty() {
  return (
    <Alert>
      <AlertTitle>No analytics events yet</AlertTitle>
      <AlertDescription>Once the desktop app sends an event, activity and breakdowns will appear here.</AlertDescription>
    </Alert>
  );
}

export function DashboardError({ onRetry }: DashboardStateProps) {
  return (
    <Alert className="border-amber-300 bg-amber-50">
      <AlertTitle>Unable to load analytics</AlertTitle>
      <AlertDescription className="flex flex-wrap items-center gap-3">
        Check the local analytics database, then retry this request.
        {onRetry ? <Button type="button" variant="secondary" size="sm" onClick={onRetry}>Retry</Button> : null}
      </AlertDescription>
    </Alert>
  );
}
