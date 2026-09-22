import { timingSafeEqual } from "node:crypto";
import { NextResponse } from "next/server";
import { createAccessSession, SESSION_TTL_SECONDS } from "../../../lib/access-session.ts";

const ACCESS_COOKIE = "sdm_admin_access";

function secretsMatch(provided: string, expected: string): boolean {
  const providedBytes = Buffer.from(provided);
  const expectedBytes = Buffer.from(expected);
  return providedBytes.length === expectedBytes.length && timingSafeEqual(providedBytes, expectedBytes);
}

export async function POST(request: Request): Promise<Response> {
  const expected = process.env.ADMIN_ACCESS_SECRET;
  if (!expected) {
    return Response.json(
      { ok: false, error: { code: "ADMIN_ACCESS_NOT_CONFIGURED", message: "Admin access is not configured." } },
      { status: 503 },
    );
  }

  let body: unknown;
  try {
    body = await request.json();
  } catch {
    return Response.json(
      { ok: false, error: { code: "INVALID_REQUEST", message: "Request body must contain JSON." } },
      { status: 400 },
    );
  }

  const secret = body && typeof body === "object" && "secret" in body && typeof body.secret === "string" ? body.secret : "";
  if (!secretsMatch(secret, expected)) {
    return Response.json(
      { ok: false, error: { code: "UNAUTHORIZED", message: "Admin access is required." } },
      { status: 401 },
    );
  }

  const response = NextResponse.json({ ok: true });
  response.cookies.set({
    name: ACCESS_COOKIE,
    value: await createAccessSession(expected),
    httpOnly: true,
    sameSite: "strict",
    secure: process.env.NODE_ENV === "production",
    path: "/",
    maxAge: SESSION_TTL_SECONDS,
  });
  return response;
}
