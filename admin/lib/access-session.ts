const SESSION_TTL_SECONDS = 60 * 60 * 8;

function encode(value: string): string {
  return Buffer.from(value).toString("base64url");
}

async function hmacKey(secret: string, usage: KeyUsage): Promise<CryptoKey> {
  return crypto.subtle.importKey(
    "raw",
    new TextEncoder().encode(secret),
    { name: "HMAC", hash: "SHA-256" },
    false,
    [usage],
  );
}

async function sign(payload: string, secret: string): Promise<string> {
  const signature = await crypto.subtle.sign("HMAC", await hmacKey(secret, "sign"), new TextEncoder().encode(payload));
  return Buffer.from(signature).toString("base64url");
}

export async function createAccessSession(secret: string, now = Date.now()): Promise<string> {
  const payload = encode(JSON.stringify({ exp: Math.floor(now / 1000) + SESSION_TTL_SECONDS }));
  return `${payload}.${await sign(payload, secret)}`;
}

export async function isValidAccessSession(token: string | undefined, secret: string, now = Date.now()): Promise<boolean> {
  if (!token) return false;
  const [payload, signature, extra] = token.split(".");
  if (!payload || !signature || extra) return false;
  const isSigned = await crypto.subtle.verify(
    "HMAC",
    await hmacKey(secret, "verify"),
    Buffer.from(signature, "base64url"),
    new TextEncoder().encode(payload),
  );
  if (!isSigned) return false;
  try {
    const value: unknown = JSON.parse(Buffer.from(payload, "base64url").toString());
    return typeof value === "object" && value !== null && "exp" in value && typeof value.exp === "number" && Number.isSafeInteger(value.exp) && value.exp > Math.floor(now / 1000);
  } catch {
    return false;
  }
}

export { SESSION_TTL_SECONDS };
