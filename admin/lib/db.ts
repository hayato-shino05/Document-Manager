import "server-only";
import postgres from "postgres";

export type SqlClient = postgres.Sql;

let database: SqlClient | undefined;

export function getDatabase(): SqlClient {
  if (database) {
    return database;
  }

  const connectionString = process.env.DATABASE_URL;
  if (!connectionString) {
    throw new Error("DATABASE_URL is not configured");
  }

  database = postgres(connectionString, {
    connect_timeout: 10,
    idle_timeout: 20,
    max: 5,
    prepare: false,
  });

  return database;
}

export function createDatabase(connectionString: string): SqlClient {
  if (!connectionString.trim()) {
    throw new Error("A PostgreSQL connection string is required");
  }

  return postgres(connectionString, {
    connect_timeout: 10,
    idle_timeout: 20,
    max: 1,
    prepare: false,
  });
}
