# 5. Code-first OpenAPI contract, generated client types

- Status: Accepted
- Date: 2026-11-27

## Context

The Angular app and the API change together, slice by slice. Hand-written TypeScript copies of the DTOs drift
apart without anyone noticing. Writing the OpenAPI document by hand first would mean keeping the controllers in
line with it by hand.

## Decision

- **The controllers and request/response DTOs are the source of the contract.**
  - `Microsoft.AspNetCore.OpenApi` describes them.
  - `ProducesResponseType` attributes declare every status, including the ProblemDetails responses.
  - DataAnnotations on the DTOs become schema constraints.
- **The document is generated at build time.** `Microsoft.Extensions.ApiDescription.Server` writes it to
  `backend/openapi/claims-api.json`, and the file is **committed**. Docker image builds skip generation
  (`OpenApiGenerateDocuments=false`).
- **The frontend generates its types from that file.**
  - `npm run gen:api` runs `openapi-typescript` and writes `frontend/src/app/core/api/api-types.ts`.
  - Every `*.api.ts` uses only those types. Hand-written DTO interfaces are not allowed.
- **Scalar** serves interactive docs at `/scalar` in Development.

## Consequences

- **+** One source of truth. A renamed or retyped field breaks the Angular build instead of failing at runtime.
- **+** The committed spec shows contract changes in code review.
- **−** Someone has to rebuild the backend and run `gen:api` after an API change. A stale spec stays stale until then.
  A CI check comparing a fresh spec with the committed one would close this gap. It was not built.
- **−** Some generated shapes need care. For example, .NET 10 types `ProblemDetails.status` as
  `number | string | null`, so the error interceptor reads the HTTP status instead.
- **−** `openapi-typescript` needs a scoped npm `overrides` entry, because it declares TypeScript 5 as a peer
  dependency and the project uses TypeScript 6.

## Alternatives considered

- **Spec-first (write the OpenAPI file, generate server stubs):** more ceremony, and the C# stubs fight the
  thin-controller style.
- **A generated client (NSwag, `openapi-generator`):** a heavier runtime dependency. Types plus a thin
  `HttpClient` wrapper per feature are enough.
- **Hand-written interfaces:** drift. Ruled out by CLAUDE.md.
