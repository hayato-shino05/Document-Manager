import { NextResponse } from "next/server.js";
import type { NextRequest } from "next/server.js";
import { isValidAccessSession } from "./lib/access-session.ts";

const ACCESS_COOKIE = "sdm_admin_access";

function isProtectedPath(pathname: string): boolean {
  return pathname === "/" || pathname.startsWith("/monthly") || pathname.startsWith("/api/analytics/");
}

export async function proxy(request: NextRequest) {
  if (!isProtectedPath(request.nextUrl.pathname)) {
    return NextResponse.next();
  }

  const secret = process.env.ADMIN_ACCESS_SECRET;
  if (!secret) {
    if (request.nextUrl.pathname.startsWith("/api/analytics/")) {
      return Response.json(
        { ok: false, error: { code: "ADMIN_ACCESS_NOT_CONFIGURED", message: "Admin access is not configured." } },
        { status: 503 },
      );
    }

    return NextResponse.redirect(new URL("/access?error=not-configured", request.url));
  }

  if (await isValidAccessSession(request.cookies.get(ACCESS_COOKIE)?.value, secret)) {
    return NextResponse.next();
  }

  if (request.nextUrl.pathname.startsWith("/api/analytics/")) {
    return Response.json(
      { ok: false, error: { code: "UNAUTHORIZED", message: "Admin access is required." } },
      { status: 401 },
    );
  }

  return NextResponse.redirect(new URL("/access", request.url));
}

export const config = {
  matcher: ["/", "/monthly/:path*", "/api/analytics/:path*"],
};
