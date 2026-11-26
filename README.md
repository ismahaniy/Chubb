# ClaimDesk

Claims platform for motor and property claims across six APAC markets.
Claimants report and track claims, officers assess and settle them, and managers see workload and exposure.

> Work in progress: this is the repository scaffold. Features arrive slice by slice.

## Prerequisites

- .NET SDK 10.0.404+ (pinned in `global.json`)
- Node.js 22+ and npm
- Docker with Compose v2

## Run

```bash
# Infrastructure: PostgreSQL 17, Kafka 4.1 (KRaft), topic init, Kafka UI
docker compose up -d          # optional: cp .env.example .env to override ports/credentials

# Backend
dotnet build backend
dotnet test backend
dotnet watch --project backend/src/Claims.Api      # http://localhost:5000

# Frontend
npm install --prefix frontend
npm start --prefix frontend                         # http://localhost:4200, /api proxied to :5000
npm test --prefix frontend
```

## URLs

| What        | URL                          |
|-------------|------------------------------|
| Frontend    | http://localhost:4200        |
| API         | http://localhost:5000        |
| API health  | http://localhost:5000/health |
| Kafka UI    | http://localhost:8080        |
| PostgreSQL  | localhost:5432 (claims/claims) |
| Kafka (host)| localhost:9092 (containers: kafka:29092) |

## Layout

See `CLAUDE.md` for architecture and conventions, and `docs/` for ADRs and the AI journal.
