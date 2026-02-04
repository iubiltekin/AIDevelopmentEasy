# Planner – Rust

When this codebase uses Rust:

- Use file extension `.rs` for Rust files in `target_files`.
- **Module layout**: One module per file; file path = module path (e.g. `src/services/auth.rs` → `mod services::auth` or `use crate::services::auth`). Follow the existing `mod` tree in the codebase context.
- **Crate structure**: Prefer `src/lib.rs` for library roots and `src/main.rs` for binaries. Put new code in `src/` submodules (e.g. `src/domain/`, `src/application/`) to match context.
- **Naming**: `snake_case` for functions, modules, and files; `PascalCase` for types, traits, and enums. Match existing style in context.
- **Error handling**: Prefer `Result<T, E>` and existing error types (e.g. `thiserror`, `anyhow`) if present. Do not introduce a new error crate unless required.
- **Async**: Use `async`/`.await` and the same runtime (e.g. `tokio`) as the rest of the crate. Check `Cargo.toml` and context for runtime and traits.
- **Tests**: Unit tests in the same file under `#[cfg(test)]`; integration tests in `tests/*.rs`. Match the project's test layout.
- **Namespace**: In task output, use the crate name and module path (e.g. `crate_name::module::SubModule`) as in the codebase context.
