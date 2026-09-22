"use client";

import { useRouter } from "next/navigation";
import type { ComponentProps } from "react";
import { useState } from "react";

export default function AccessPage() {
  const router = useRouter();
  const [secret, setSecret] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [pending, setPending] = useState(false);

  async function submit(event: Parameters<NonNullable<ComponentProps<"form">["onSubmit"]>>[0]) {
    event.preventDefault();
    setPending(true);
    setError(null);

    try {
      const response = await fetch("/api/access", {
        method: "POST",
        headers: { "content-type": "application/json" },
        body: JSON.stringify({ secret }),
      });
      const body: unknown = await response.json();
      if (!response.ok || !body || typeof body !== "object" || !("ok" in body) || body.ok !== true) {
        setError("Access was not granted. Check the configured admin secret.");
        return;
      }
      router.replace("/");
    } catch {
      setError("Access could not be verified. Try again.");
    } finally {
      setPending(false);
    }
  }

  return (
    <main className="flex min-h-screen items-center justify-center bg-stone-50 px-6 py-12">
      <section className="w-full max-w-md rounded-lg border border-stone-200 bg-white p-8 shadow-sm">
        <p className="text-xs font-semibold uppercase tracking-[0.18em] text-teal-800">Document Manager</p>
        <h1 className="mt-2 text-2xl font-semibold tracking-tight text-stone-900">Analytics access</h1>
        <p className="mt-3 text-sm leading-6 text-stone-600">Enter the admin access secret to open the analytics dashboard.</p>
        <form className="mt-6 grid gap-4" onSubmit={submit}>
          <label className="grid gap-2 text-sm font-medium text-stone-700">
            Access secret
            <input
              type="password"
              value={secret}
              onChange={(event) => setSecret(event.target.value)}
              autoComplete="current-password"
              required
              className="min-h-11 rounded-md border border-stone-300 px-3 text-stone-900 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-teal-700/50"
            />
          </label>
          {error ? <p role="alert" className="text-sm text-red-700">{error}</p> : null}
          <button type="submit" disabled={pending} className="min-h-11 rounded-md bg-teal-800 px-4 font-medium text-white hover:bg-teal-900 disabled:cursor-not-allowed disabled:opacity-60">
            {pending ? "Checking…" : "Open dashboard"}
          </button>
        </form>
      </section>
    </main>
  );
}
