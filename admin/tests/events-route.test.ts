import assert from "node:assert/strict";
import test from "node:test";
import { handlePost } from "../app/api/events/route.ts";

type EventWriter = {
  insertEvent(input: Record<string, unknown>): Promise<boolean>;
};

function request(body: unknown, headers?: HeadersInit): Request {
  return new Request("http://localhost/api/events", {
    method: "POST",
    headers: { "content-type": "application/json", ...headers },
    body: typeof body === "string" ? body : JSON.stringify(body),
  });
}

function writer(capture: (input: Record<string, unknown>) => void = () => undefined): EventWriter {
  return {
    async insertEvent(input) {
      capture(input);
      return true;
    },
  };
}

const validEvent = {
  installation_id: "11111111-1111-4111-8111-111111111111",
  event: "app_opened",
  app_version: "4.0.0",
  platform: "windows",
  occurred_at: "2026-08-09T14:30:00+02:00",
  properties: { source: "startup", success: true },
};

test("accepts a valid event and normalizes the timestamp before persistence", async () => {
  let received: Record<string, unknown> | undefined;
  const response = await handlePost(request(validEvent), writer((input) => { received = input; }));

  assert.equal(response.status, 202);
  assert.deepEqual(await response.json(), { ok: true });
  assert.equal((received?.occurredAt as Date).toISOString(), "2026-08-09T12:30:00.000Z");
  assert.deepEqual(received?.properties, validEvent.properties);
});

test("rejects malformed UUIDs", async () => {
  const response = await handlePost(request({ ...validEvent, installation_id: "not-a-uuid" }), writer());

  assert.equal(response.status, 400);
  assert.equal((await response.json()).error.code, "INVALID_EVENT");
});

test("rejects unknown event names", async () => {
  const response = await handlePost(request({ ...validEvent, event: "unknown_event" }), writer());

  assert.equal(response.status, 400);
  assert.equal((await response.json()).error.code, "INVALID_EVENT");
});

test("rejects rollover calendar dates", async () => {
  const response = await handlePost(request({ ...validEvent, occurred_at: "2026-02-30T12:00:00Z" }), writer());

  assert.equal(response.status, 400);
  assert.equal((await response.json()).error.code, "INVALID_EVENT");
});

test("limits streamed bodies without relying on Content-Length", async () => {
  const chunk = new TextEncoder().encode("x".repeat(16 * 1024));
  const requestWithStream = new Request("http://localhost/api/events", {
    method: "POST",
    headers: { "content-type": "application/json" },
    body: new ReadableStream({
      start(controller) {
        controller.enqueue(chunk);
        controller.enqueue(new Uint8Array([120]));
        controller.close();
      },
    }),
    duplex: "half",
  } as RequestInit);
  const response = await handlePost(requestWithStream, writer());

  assert.equal(response.status, 413);
  assert.equal((await response.json()).error.code, "PAYLOAD_TOO_LARGE");
});

test("rejects forbidden property names", async () => {
  const response = await handlePost(request({ ...validEvent, properties: { name: "private title" } }), writer());

  assert.equal(response.status, 400);
  assert.equal((await response.json()).error.code, "INVALID_EVENT");
});

test("rejects oversized request bodies before parsing", async () => {
  const response = await handlePost(
    request("{}", { "content-length": String(16 * 1024 + 1) }),
    writer(),
  );

  assert.equal(response.status, 413);
  assert.equal((await response.json()).error.code, "PAYLOAD_TOO_LARGE");
});

test("returns a safe envelope when persistence fails", async () => {
  const failingWriter: EventWriter = {
    async insertEvent() {
      throw new Error("database credentials should not escape");
    },
  };
  const response = await handlePost(request(validEvent), failingWriter);

  assert.equal(response.status, 500);
  assert.deepEqual(await response.json(), {
    ok: false,
    error: { code: "INTERNAL_ERROR", message: "Unable to record event." },
  });
});
