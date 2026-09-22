# Document Manager Admin Analytics

Document Manager 向けの Next.js 製 analytics dashboard です。PostgreSQL に保存した集計データを表示し、デスクトップアプリからの event ingestion には `POST /api/events` を使います。

## Local setup

1. Copy `.env.example` to `.env.local`.
2. Set `DATABASE_URL` to the PostgreSQL connection string.
3. Set `ADMIN_ACCESS_SECRET` to a long random value. The dashboard and analytics API fail closed when this value is missing in a deployment.
4. Run:

```bash
npm install
npm run dev
```

Open `http://localhost:3000`. The access form creates an HttpOnly, same-site cookie for the dashboard session. This is a small deployment gate, not a replacement for a full identity provider or role-based authentication.

The ingestion route remains unauthenticated so installed desktop clients can send events. Validate and rate-limit it at the hosting/network boundary if it is exposed beyond the intended clients.

## Vercel monorepo settings

Create a Vercel project from the repository and set **Root Directory** to `admin`. No `vercel.json` is required for this layout. Use the following project settings:

- Framework preset: Next.js
- Install command: `npm install`
- Build command: `npm run build`
- Output directory: leave the Next.js default

Set these Vercel Environment Variables for the relevant environments. Never commit real values:

- `DATABASE_URL`: server-only PostgreSQL connection string.
- `ADMIN_ACCESS_SECRET`: long random secret used by the server-side dashboard access gate. Do not use a `NEXT_PUBLIC_` prefix.

After deployment, configure the desktop application setting `STUDY_DOCUMENT_ANALYTICS_URL` with the deployment URL plus `/api/events`, for example `https://admin.example.com/api/events`. This value belongs to the desktop application's runtime environment and is documented in `.env.example` for setup reference.

## Checks

```bash
npm run lint
npm run typecheck
npm test
npm run build
```

Do not run a deployment from this repository without explicit authorization and Vercel credentials.
