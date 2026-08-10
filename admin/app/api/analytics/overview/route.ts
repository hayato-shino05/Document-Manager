import type { AnalyticsRepository } from "../../../../lib/analytics-repository.ts";
import {
  AnalyticsRangeError,
  loadAnalyticsData,
  parseDateRange,
  type AnalyticsReader,
} from "../../../../lib/analytics-query.ts";

type ErrorCode = "INVALID_RANGE" | "INTERNAL_ERROR";

type OverviewReader = Pick<AnalyticsRepository, "getOverview" | "getMonthlyReport" | "getMonthlyAggregates" | "getBreakdowns">;

function errorResponse(status: 400 | 500, code: ErrorCode, message: string): Response {
  return Response.json({ ok: false, error: { code, message } }, { status });
}

async function createReader(): Promise<OverviewReader> {
  const [{ getDatabase }, { AnalyticsRepository }] = await Promise.all([
    import("../../../../lib/db.ts"),
    import("../../../../lib/analytics-repository.ts"),
  ]);
  return new AnalyticsRepository(getDatabase());
}

export async function handleGet(request: Request, reader?: AnalyticsReader): Promise<Response> {
  let range;
  try {
    range = parseDateRange(request);
  } catch (error) {
    if (error instanceof AnalyticsRangeError) return errorResponse(400, "INVALID_RANGE", error.message);
    return errorResponse(400, "INVALID_RANGE", "Date range is invalid.");
  }

  try {
    const data = await loadAnalyticsData(reader ?? (await createReader()), range);
    return Response.json({ ok: true, data });
  } catch {
    return errorResponse(500, "INTERNAL_ERROR", "Unable to load analytics.");
  }
}

export async function GET(request: Request): Promise<Response> {
  return handleGet(request);
}
