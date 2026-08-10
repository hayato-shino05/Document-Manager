import {
  AnalyticsContractError,
  parseAnalyticsEvent,
  type NormalizedAnalyticsEvent,
} from "../../../lib/analytics-contract.ts";
import type { AnalyticsRepository } from "../../../lib/analytics-repository.ts";

const MAX_BODY_BYTES = 16 * 1024;

type EventWriter = Pick<AnalyticsRepository, "insertEvent">;

type ErrorCode = "INVALID_JSON" | "INVALID_EVENT" | "PAYLOAD_TOO_LARGE" | "INTERNAL_ERROR";

function errorResponse(status: 400 | 413 | 500, code: ErrorCode, message: string): Response {
  return Response.json({ ok: false, error: { code, message } }, { status });
}

function acceptedResponse(): Response {
  return Response.json({ ok: true }, { status: 202 });
}

async function readBodyWithinLimit(request: Request): Promise<string> {
  if (!request.body) return "";
  const reader = request.body.getReader();
  const chunks: Uint8Array[] = [];
  let total = 0;
  try {
    while (true) {
      const { done, value } = await reader.read();
      if (done) break;
      total += value.byteLength;
      if (total > MAX_BODY_BYTES) {
        await reader.cancel();
        throw new RangeError("PAYLOAD_TOO_LARGE");
      }
      chunks.push(value);
    }
  } finally {
    reader.releaseLock();
  }
  const body = new Uint8Array(total);
  let offset = 0;
  for (const chunk of chunks) {
    body.set(chunk, offset);
    offset += chunk.byteLength;
  }
  return new TextDecoder().decode(body);
}

async function createEventWriter(): Promise<EventWriter> {
  const [{ getDatabase }, { AnalyticsRepository }] = await Promise.all([
    import("../../../lib/db.ts"),
    import("../../../lib/analytics-repository.ts"),
  ]);
  return new AnalyticsRepository(getDatabase());
}

function toRepositoryInput(event: NormalizedAnalyticsEvent) {
  return {
    installationId: event.installationId,
    eventName: event.eventName,
    appVersion: event.appVersion,
    platform: event.platform,
    occurredAt: event.occurredAt,
    properties: event.properties,
  };
}

export async function handlePost(request: Request, writer?: EventWriter): Promise<Response> {
  const declaredLength = request.headers.get("content-length");
  if (declaredLength !== null) {
    const length = Number(declaredLength);
    if (!Number.isSafeInteger(length) || length < 0) {
      return errorResponse(400, "INVALID_JSON", "Content-Length must be valid.");
    }
    if (length > MAX_BODY_BYTES) {
      return errorResponse(413, "PAYLOAD_TOO_LARGE", "Request body is too large.");
    }
  }

  let body: string;
  try {
    body = await readBodyWithinLimit(request);
  } catch (error) {
    if (error instanceof RangeError && error.message === "PAYLOAD_TOO_LARGE") {
      return errorResponse(413, "PAYLOAD_TOO_LARGE", "Request body is too large.");
    }
    return errorResponse(400, "INVALID_JSON", "Request body could not be read.");
  }

  let parsed: unknown;
  try {
    parsed = JSON.parse(body);
  } catch {
    return errorResponse(400, "INVALID_JSON", "Request body must contain valid JSON.");
  }

  let event: NormalizedAnalyticsEvent;
  try {
    event = parseAnalyticsEvent(parsed);
  } catch (error) {
    if (error instanceof AnalyticsContractError) {
      return errorResponse(400, error.code, error.message);
    }
    return errorResponse(400, "INVALID_EVENT", "Request body is invalid.");
  }

  try {
    await (writer ?? (await createEventWriter())).insertEvent(toRepositoryInput(event));
  } catch {
    return errorResponse(500, "INTERNAL_ERROR", "Unable to record event.");
  }

  return acceptedResponse();
}

export async function POST(request: Request): Promise<Response> {
  return handlePost(request);
}
