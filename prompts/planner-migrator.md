# Planner – Database migrations (migrator)

Use this when the codebase has a **database migrator** (e.g. Go migrator, `psqlmigrations` or similar) that runs ordered up/down SQL migrations.

## When this applies

- The codebase context mentions a **migrator** application and a **migrations directory** (e.g. `scripts/migrator`, `psqlmigrations`, `migrations`, or path from config `migration_path`).
- Requirements involve **schema changes**, new tables, columns, indexes, or data migrations.

## Migration file format

- **Naming**: `NNNNNN_descriptive_name.up.sql` and `NNNNNN_descriptive_name.down.sql`
  - `NNNNNN`: 6-digit zero-padded version (e.g. `000042`)
  - `descriptive_name`: snake_case, short and clear (e.g. `add_user_preferences`, `campaign_categories`)
- **Location**: In the migrations directory used by the migrator (e.g. `psqlmigrations/`, or path from codebase context).
- **Pairing**: Every migration must have both `.up.sql` (apply) and `.down.sql` (rollback). If `down` is missing, the migrator may skip rollback for that version.

## Version numbering

- Versions are **sequential**. The next version is **one more than the highest version** already present in the migrations directory.
- From the codebase context, read the existing migration filenames (e.g. `000041_*.sql`, `000042_*.sql`) and plan new migrations with the **next** version number (e.g. `000043_...`).
- Do not reuse or skip version numbers.

## Task structure for migrations

1. **One logical migration = one version number** with two files: `NNNNNN_name.up.sql` and `NNNNNN_name.down.sql`.
2. In the plan, treat each new migration as a single task (or one task per migration) with:
   - `target_files`: e.g. `["psqlmigrations/000043_add_feature_x.up.sql", "psqlmigrations/000043_add_feature_x.down.sql"]` (paths as in codebase context)
   - `modification_type`: `create`
   - Clear description of the schema change; the Coder will write both up and down SQL.
3. **Order tasks**: Migration tasks first (by version order), then any application code that depends on the new schema.

## SQL and migrator behavior

- **Up**: Applies the change (CREATE TABLE, ALTER TABLE, CREATE INDEX, etc.).
- **Down**: Reverses the change so that running down then up leaves the DB as before.
- **Idempotency** (when possible): Prefer SQL that is safe to run more than once (e.g. `IF NOT EXISTS`, `ALTER TABLE ... ADD COLUMN IF NOT EXISTS`, or conditional PL/pgSQL blocks). The codebase README may describe idempotency patterns.
- **PostgreSQL**: Use standard PostgreSQL syntax; the example migrator uses `golang-migrate` with the postgres driver. Avoid DB-specific extensions unless the codebase already uses them.
- **schema_migrations**: The migrator tracks applied version in a table (e.g. `schema_migrations`). Do not modify that table in migration files.

## Project layout (example)

- Migrator app: e.g. `scripts/migrator/main.go` (reads config, runs up/down from migration path).
- Migrations: e.g. `scripts/migrator/psqlmigrations/` or path from config `db.migration_path`.
- Config: migration path and DB connection come from config; migrations are only file paths under that directory.

## Output format (tasks)

For each new migration, include in the task:

- `target_files`: both `.../NNNNNN_name.up.sql` and `.../NNNNNN_name.down.sql`
- `namespace` / `project`: the project or module that contains the migrator (from context)
- Description: what schema change the up migration does and what the down migration must undo

Keep migration tasks **small and single-purpose** (e.g. one table, one index, one set of columns) so versions stay easy to reason about and roll back.
