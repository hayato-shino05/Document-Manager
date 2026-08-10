import assert from "node:assert/strict";
import test from "node:test";
import { NextRequest } from "next/server.js";
import { proxy } from "../proxy.ts";
import { createAccessSession, isValidAccessSession } from "../lib/access-session.ts";

test("dashboard proxy redirects unauthenticated requests", async () => {
  process.env.ADMIN_ACCESS_SECRET = "test-secret";
  const response = await proxy(new NextRequest("https://example.test/"));
  assert.equal(response.status, 307);
  assert.equal(response.headers.get("location"), "https://example.test/access");
});

test("dashboard proxy allows a signed access session", async () => {
  process.env.ADMIN_ACCESS_SECRET = "test-secret";
  const session = await createAccessSession("test-secret");
  assert.notEqual(session, "test-secret");
  const request = new NextRequest("https://example.test/api/analytics/overview", {
    headers: { cookie: `sdm_admin_access=${session}` },
  });
  const response = await proxy(request);
  assert.equal(response.headers.get("x-middleware-next"), "1");
});

test("dashboard proxy fails closed when no access secret is configured", async () => {
  delete process.env.ADMIN_ACCESS_SECRET;
  const response = await proxy(new NextRequest("https://example.test/api/analytics/overview"));
  assert.equal(response.status, 503);
});

test("signed access sessions reject expired and tampered tokens", async () => {
  const session = await createAccessSession("test-secret", 0);
  assert.equal(await isValidAccessSession(session, "test-secret", 8 * 60 * 60 * 1000 + 1), false);
  const valid = await createAccessSession("test-secret");
  assert.equal(await isValidAccessSession(`${valid}x`, "test-secret"), false);
});
