# LiftLedger

UK SaaS scaffold for **LOLER / PUWER / trailer and plant inspection** records. Built for workshops, hire fleets and examiners.

LiftLedger stores thorough-examination working records (Schedule 1 style: date, examiner, SWL, defects, next due) and fleet assets with QR/ID codes. **It is not an HSE-certified product** and does not determine legal compliance. A competent person remains responsible under LOLER 1998, PUWER 1998 and the Health and Safety at Work etc. Act 1974.

## Monorepo

| Path | Stack |
|------|--------|
| `apps/api` | ASP.NET Core 8 Web API, EF Core, JWT |
| `apps/mobile` | React Native (Expo managed workflow) |
| `LiftLedger.sln` | API + tests |

Default data store is **SQLite** for local development. SQL Server is supported by configuration (see below).

## Tenant isolation

A **tenant** is a workshop or hire company. Registering creates that tenant plus an **Owner** user.

- JWT includes `tenant_id` and `role` (`Owner`, `Admin`, `Examiner`, `Viewer`).
- Every business entity (`Membership`, `Client`, `Site`, `Asset`, `Inspection`, `Defect`) has `TenantId`.
- EF Core **global query filters** restrict reads to the signed-in tenant.
- `SaveChanges` **blocks cross-tenant writes** when a user is authenticated.
- Services/controllers also call `EnsureTenant` so a guessed ID returns **404**, not another organisation’s record.

Do not treat query filters as the only control. Always keep `[Authorize]` on business endpoints and the explicit tenant checks.

## API

```bash
export DOTNET_ROOT=$HOME/.dotnet   # if the SDK is user-installed
cd apps/api/LiftLedger.Api
dotnet run --launch-profile http
```

- API: http://localhost:5080
- Swagger (Development): http://localhost:5080/swagger
- Health: `GET /health`

### Demo seed (Development)

| Organisation | Email | Password | Role |
|--------------|-------|----------|------|
| Humber Plant & Trailer Hire Ltd | `owner@humberhire.demo` | `DemoPass123!` | Owner |
| Humber Plant & Trailer Hire Ltd | `examiner@humberhire.demo` | `DemoPass123!` | Examiner |
| Northern Plant Examiners Ltd | `owner@northernpplant.demo` | `DemoPass123!` | Owner |

Use the two owner accounts to confirm tenant isolation: Humber assets (e.g. `TRI-1042`) must not appear when signed in as Northern.

### Auth

- `POST /api/auth/register` — organisation name, owner email/password → tenant + JWT
- `POST /api/auth/login`
- `GET /api/auth/me`

Roles: **Owner** and **Admin** manage clients/sites and archive assets; **Examiner** can create assets and complete examinations; **Viewer** is read-only.

### SQL Server instead of SQLite

```bash
docker compose up -d sqlserver
```

Then run the API with:

```bash
Database__Provider=SqlServer
Database__ConnectionString="Server=localhost,1433;Database=LiftLedger;User Id=sa;Password=Your_password123;TrustServerCertificate=True"
Jwt__SigningKey="a-production-key-of-at-least-32-characters"
```

`EnsureCreated` is used for this scaffold so SQLite and SQL Server both boot without a migration pipeline. Before production, add EF migrations (`dotnet ef migrations add`) and switch startup to `Database.Migrate()`.

Production **must** set `Jwt__SigningKey` (32+ characters). Do not commit secrets.

## Mobile (Expo)

```bash
cd apps/mobile
cp .env.example .env
# Android emulator: EXPO_PUBLIC_API_URL=http://10.0.2.2:5080
# Physical device: use your machine’s LAN IP
npx expo start
```

Screens: sign in / register, home dashboard (overdue, due soon, recent examinations), assets list/detail/add, start and complete inspection (offline copy saved on the device if the API is unreachable), examination history, settings.

## Tests

```bash
dotnet test LiftLedger.sln
```

Coverage includes register → tenant JWT claims, create asset + complete inspection, second-tenant 404s, and dashboard due/overdue lists.

## Regulation-aware wording

Certificate HTML/PDF files are labelled as LiftLedger working records in the style of LOLER Schedule 1. They state that they are **not HSE-certified** and are **not a legal determination of compliance**.
