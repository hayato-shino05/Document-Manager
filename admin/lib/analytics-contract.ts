export const ALLOWED_EVENT_NAMES = [
  "app_opened",
  "session_started",
  "app_closed",
  "document_added",
  "document_opened",
  "batch_import_completed",
  "export_completed",
  "app_updated",
] as const;

export type AllowedEventName = (typeof ALLOWED_EVENT_NAMES)[number];

export const ALLOWED_PROPERTY_KEYS = [
  "count",
  "duration_ms",
  "file_count",
  "item_count",
  "mode",
  "reason",
  "result",
  "source",
  "success",
] as const;

const ALLOWED_EVENT_NAME_SET = new Set<string>(ALLOWED_EVENT_NAMES);
const ALLOWED_PROPERTY_KEY_SET = new Set<string>(ALLOWED_PROPERTY_KEYS);

function isAllowedEventName(value: string): value is AllowedEventName {
  return ALLOWED_EVENT_NAME_SET.has(value);
}
const FORBIDDEN_PROPERTY_KEYS = new Set([
  "name",
  "path",
  "file_path",
  "notes",
  "content",
  "tags",
  "document_id",
  "database",
]);
const UUID_PATTERN = /^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i;
const ISO_TIMESTAMP_PATTERN = /^(\d{4})-(\d{2})-(\d{2})T\d{2}:\d{2}:\d{2}(?:\.\d{1,9})?(?:Z|[+-]\d{2}:?\d{2})$/;
const MAX_EVENT_NAME_LENGTH = 64;
const MAX_VERSION_LENGTH = 32;
const MAX_PROPERTY_COUNT = 16;

export type AnalyticsEventPayload = {
  installation_id: string;
  event: AllowedEventName;
  app_version: string;
  platform: "windows";
  occurred_at: string;
  properties?: Record<string, number | boolean | string>;
};

export type NormalizedAnalyticsEvent = {
  installationId: string;
  eventName: AllowedEventName;
  appVersion: string;
  platform: "windows";
  occurredAt: Date;
  properties: Record<string, number | boolean | string>;
};

export class AnalyticsContractError extends Error {
  readonly code: "INVALID_JSON" | "INVALID_EVENT";

  constructor(code: "INVALID_JSON" | "INVALID_EVENT", message: string) {
    super(message);
    this.name = "AnalyticsContractError";
    this.code = code;
  }
}

function invalid(message: string): never {
  throw new AnalyticsContractError("INVALID_EVENT", message);
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}

function parseEventName(value: unknown): AllowedEventName {
  if (typeof value !== "string" || value.length === 0 || value.length > MAX_EVENT_NAME_LENGTH || !isAllowedEventName(value)) {
    invalid("event is not supported.");
  }
  return value;
}

export function parseAnalyticsEvent(value: unknown): NormalizedAnalyticsEvent {
  if (!isRecord(value)) {
    return invalid("Request body must be a JSON object.");
  }

  const allowedFields = new Set(["installation_id", "event", "app_version", "platform", "occurred_at", "properties"]);
  for (const key of Object.keys(value)) {
    if (!allowedFields.has(key)) {
      invalid("Request contains an unsupported field.");
    }
  }

  const installationId = value.installation_id;
  if (typeof installationId !== "string" || !UUID_PATTERN.test(installationId)) {
    invalid("installation_id must be a valid UUID.");
  }

  const event = parseEventName(value.event);

  const appVersion = value.app_version;
  if (typeof appVersion !== "string" || appVersion.trim().length === 0 || appVersion.length > MAX_VERSION_LENGTH) {
    invalid("app_version must be a non-empty string of at most 32 characters.");
  }

  if (value.platform !== "windows") {
    invalid("platform must be windows.");
  }

  const occurredAt = value.occurred_at;
  const timestampMatch = typeof occurredAt === "string" ? ISO_TIMESTAMP_PATTERN.exec(occurredAt) : null;
  if (typeof occurredAt !== "string" || !timestampMatch) {
    invalid("occurred_at must be an ISO 8601 timestamp.");
  }
  const [year, month, day] = timestampMatch.slice(1, 4).map(Number);
  const calendarDate = new Date(Date.UTC(year, month - 1, day));
  if (calendarDate.getUTCFullYear() !== year || calendarDate.getUTCMonth() !== month - 1 || calendarDate.getUTCDate() !== day) {
    invalid("occurred_at must contain a valid calendar date.");
  }
  const normalizedOccurredAt = new Date(occurredAt);
  if (Number.isNaN(normalizedOccurredAt.getTime())) {
    invalid("occurred_at must be a valid timestamp.");
  }

  const properties: Record<string, number | boolean | string> = {};
  if (value.properties !== undefined) {
    if (!isRecord(value.properties)) {
      invalid("properties must be a JSON object.");
    }
    const propertyEntries = Object.entries(value.properties);
    if (propertyEntries.length > MAX_PROPERTY_COUNT) {
      invalid("properties contains too many fields.");
    }
    for (const [key, propertyValue] of propertyEntries) {
      if (FORBIDDEN_PROPERTY_KEYS.has(key) || !ALLOWED_PROPERTY_KEY_SET.has(key)) {
        invalid("properties contains an unsupported field.");
      }
      if (
        typeof propertyValue !== "string" &&
        typeof propertyValue !== "number" &&
        typeof propertyValue !== "boolean"
      ) {
        invalid("properties values must be strings, numbers, or booleans.");
      }
      if (typeof propertyValue === "number" && !Number.isFinite(propertyValue)) {
        invalid("properties values must be finite numbers.");
      }
      properties[key] = propertyValue;
    }
  }

  return {
    installationId,
    eventName: event,
    appVersion,
    platform: "windows",
    occurredAt: normalizedOccurredAt,
    properties,
  };
}
