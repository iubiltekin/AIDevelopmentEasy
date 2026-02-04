# Planner – React / TypeScript (frontend)

When this codebase uses React or TypeScript (frontend):

- Use file extensions `.tsx` (components with JSX), `.ts` (logic, hooks, utils), or `.jsx`/`.js` only if the project does not use TypeScript. Prefer TypeScript when present in the codebase context.
- Place components under `src/` (e.g. `src/components/`, `src/pages/`, `src/hooks/`). Match the folder structure from the codebase context.
- **Components**: PascalCase file names (e.g. `UserProfile.tsx`). One main component per file; export default the component.
- **Hooks**: `use` prefix, camelCase (e.g. `useAuth.ts`). Colocate with feature or in `src/hooks/`.
- **Utils/helpers**: camelCase file names. Prefer pure functions; put in `src/utils/` or `src/helpers/` as in context.
- **State**: Prefer existing patterns (Context, Redux, Zustand, etc.) from the codebase. Do not introduce a new state library unless the requirement explicitly asks for it.
- **Styling**: Follow the project (CSS Modules, Tailwind, styled-components, etc.) as shown in context.
- **Tests**: Same folder as source (e.g. `Component.test.tsx`) or in `__tests__`/`*.spec.tsx` if that is the project convention. Use the test framework from context (e.g. Vitest, Jest, React Testing Library).
- **Namespace / module**: Use path-based imports; no namespace field needed. `target_files` should use paths relative to `src/` (e.g. `components/Button.tsx`).
