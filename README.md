# AE Backend Code Challenge

Ship management API for the Anglo-Eastern backend challenge: users, ships, crew lists and financial reports on SQL Server. The API never talks to a table directly. Every read and write goes through a stored procedure, which is what the requirements ask for.

The ERD is in [database/ERD.md](database/ERD.md).

## Stack

ASP.NET Core 10 with controllers, Dapper for data access, SQL Server 2022, xUnit with Moq for unit tests and Testcontainers for the integration ones, Swagger UI, Docker Compose and a GitHub Actions build.

## Running it

```bash
cp .env.example .env      # set your own SA password and API key
docker compose up --build
```

API on http://localhost:8080, Swagger on http://localhost:8080/swagger.

The SQL Server image is pinned to `linux/amd64`, so on Apple Silicon it runs under emulation and the first start can take a minute or two.

Swagger UI is enabled in every environment on purpose, because compose runs the API as Production and reviewers use it through Docker. A real deployment would switch it off or put it behind auth.

The compose file has three services. `db` is SQL Server. `db-init` waits for the healthcheck, creates the `ShipManagement` database and runs every script in `database/` in filename order. `api` starts once that finishes. 

All scripts are re-runnable, tables are created behind an `IF OBJECT_ID` check, procedures use `CREATE OR ALTER`, and the seeds skip rows that already exist.

Without Docker, point `ConnectionStrings:Default` and `Auth:ApiKey` at your own instance and run:

```bash
dotnet run --project BackendCodeChallengeApi
```

The dev profile listens on http://localhost:5023.

## Auth

Every endpoint except `/health` and the OpenAPI document needs an `X-Api-Key` header. The expected key comes from `Auth:ApiKey` (`AUTH_API_KEY` in compose). Swagger has the Authorize button, so you can paste the key there and use the endpoints from the browser.

## Endpoints

| Method | Path | Notes |
| --- | --- | --- |
| POST | `/api/users` | name and role, returns 201 with a Location header |
| GET | `/api/users` | `page`, `pageSize` |
| GET | `/api/users/{userId}` | 404 when the user does not exist |
| POST | `/api/ships` | code, name, fiscal year code, optional isActive |
| GET | `/api/ships` | `status=active\|inactive\|all`, plus paging |
| GET | `/api/ships/{code}` | code is case insensitive |
| PUT | `/api/users/{userId}/ships/{code}` | assign, 204, idempotent |
| DELETE | `/api/users/{userId}/ships/{code}` | unassign, 204, idempotent |
| GET | `/api/users/{userId}/ships` | `status` filter |
| GET | `/api/ships/{code}/crew` | `page`, `pageSize`, `sortBy`, `sortDir`, `search` |
| GET | `/api/ships/{code}/financial-reports/detail?period=yyyy-MM` | one row per account, parents rolled up |
| GET | `/api/ships/{code}/financial-reports/summary?period=yyyy-MM` | monthly totals plus a YTD row |
| GET | `/health` | anonymous, checks the database connection |

## Errors

Every error is a `ProblemDetails` body (`application/problem+json`). Procedures raise their own error numbers, and `SqlErrorTranslator` maps them once, at the repository boundary.

| Status | When | Raised by |
| --- | --- | --- |
| 400 | Invalid body or query (missing field, bad period, unknown `sortBy`, page out of range). The response has an `errors` object per field. | Data annotations; procedures as `50010` |
| 401 | `X-Api-Key` missing or wrong | API key handler |
| 404 | Ship not found | `50001` |
| 404 | User not found | `50002` |
| 404 | Ship is inactive (crew list and reports only) | `50004` |
| 409 | Ship code already exists | `50003`, unique index `2627` / `2601` |
| 500 | Anything else. The detail is a generic message; the real exception is only logged. | `GlobalExceptionHandler` |

## Database

Twelve tables: `FiscalYear`, `UserRole` and `Nationality` as reference data, `Ship`, `AppUser`, `UserShip`, `CrewMember`, `CrewRank`, `CrewService` for the domain, and `Account`, `Budget`, `AccountTransaction` for the financial side. The seed gives six ships across two fiscal year patterns, 132 crew with contracts in every state, 13 ranks, 33 accounts in three levels, and budgets for 2024–2025 and actuals through September 2025 for the four active ships.

Three scalar functions and one inline table valued function do the shared work: `fn_CrewStatus`, `fn_AgeAt`, `fn_FiscalYearStart` and `fn_FinancialReport`. The last one is the report itself: it builds a closure over the account tree, reads the leaves, and rolls them up per parent, with the YTD window starting at the ship's fiscal year start.

## Architecture

```
HTTP -> controller -> repository interface -> Dapper -> dbo.usp_*
                          |
                          +-> SqlException -> SqlErrorTranslator -> DomainException -> ProblemDetails
```

Contracts are the wire format: request records with data annotations, query objects for paging, sorting and filtering, and response records with a `From(row)` mapper. Data models are the rows the procedures return. They are separate so the JSON shape can change without touching SQL, and the other way round.

Repositories are singletons. They hold no state: every method opens a connection from `IDbConnectionFactory` and disposes it when it returns.

SQL error numbers are translated once, at the repository boundary (see [Errors](#errors)). Controllers never see `SqlException`, and the exception handler turns domain exceptions into `ProblemDetails`.
## Design patterns

- Repository: one `I*Repository` interface per aggregate (`IUserRepository`, `IShipRepository`, `ICrewRepository`, `IFinancialReportRepository`), so controllers are tested with Moq and the SQL stays behind one boundary.
- Factory: `IDbConnectionFactory` / `SqlConnectionFactory` hands out an opened `SqlConnection`.
- Translator (Adapter): `SqlErrorTranslator` turns SQL error numbers into `DomainException` types, so nothing above the repository knows about `SqlException`.
- Chain of Responsibility: the ASP.NET Core middleware pipeline; `GlobalExceptionHandler` sits in it and turns domain exceptions into `ProblemDetails`.
- Strategy: authentication is an `AuthenticationHandler` (`ApiKeyAuthenticationHandler`) registered as a scheme; switching to JWT replaces the scheme, not the controllers.

## Design decisions

A thin API over stored procedures. The requirements route every database call through a stored procedure, so the procedures do the real work (crew status, account roll up, fiscal year YTD, search, sorting, paging) as set-based queries next to the data. The API only validates input, calls the procedure and shapes the response.

Not Clean Architecture or DDD. Both put the business rules in the application's centre; here those rules live in SQL, so the centre would be empty and every use case would only forward to a repository. What still pays off is kept: controllers depend on interfaces, contracts are separate from SQL rows, and names come from the requirements. When a real workflow arrives (sign on, budget import), a service is added for that endpoint. The cost is that SQL logic needs a real database to test, hence the integration tests, and it ties the solution to SQL Server.

Smaller decisions:

- API key, not JWT: there is no login flow to support, and switching means replacing one auth handler.
- Dapper, not EF Core: with stored procedures only, EF's LINQ translation and change tracking go unused; Dapper calls the procedure and maps the result.
- Crew status is computed at query time from the contract dates, not stored, so it cannot go stale.
- Fiscal year is a lookup of all twelve patterns with a check constraint, so a ship cannot get a code that breaks YTD.
- Only leaf accounts hold amounts; parents are always rolled up from them.
- `null` means no data and `0` means a recorded zero. Variance is null only when both sides are null, and the summary skips months with no figures.
- Lists default to page 1 and size 20, capped at 100; `sortBy` is checked against a fixed set of columns.

## Scalability

The API is stateless (no sessions, singleton repositories, key from configuration), so it scales out behind a load balancer. Calls are async with the request's cancellation token passed to SQL, and connections come from the pool.

The database is where load lands, so the work is shaped there: every list is paged in the procedure and capped at 100, the indexes match the access paths (open contracts per ship; budgets and transactions by ship and period, covering), and a report reads one ship's YTD window, so its cost does not grow with history.

Where it breaks first, and the next step:

- Crew search uses a leading-wildcard `LIKE`: full-text search.
- Deep `OFFSET` pages slow down: keyset paging.
- Heavy report traffic: a readable secondary, then caching.
- A very large transaction table: partition by period, or a monthly aggregate.
- One shared API key: a key per client, with rate limiting.

## Caching


Not implemented, on purpose. The only candidates are the two report endpoints, keyed by ship code and period: they do the most work, and no endpoint changes budgets or actuals, so a short TTL would never serve wrong numbers for long. The reason it is left out is that the traffic is unknown. A cache in front of a report called a few times a day adds a moving part and buys nothing. Add it when there is a traffic figure, or when budgets and actuals get a write path that would need to invalidate it.

## Assumptions

- "Today" is the UTC date. The crew list procedure takes an `@AsOfDate` parameter so tests can pin it.
- Duplicate budget or transaction rows for the same ship, account and month are summed rather than rejected.
- One holder per rank is not enforced. Handovers overlap in real life.
- An inactive ship returns 404 on crew lists and reports, with a message that says it is inactive. Ship lists still show it with its status.
- The crew list only returns Onboard and Relief Due contracts. Anything with a sign off date is excluded.
- `0` and `NULL` are different things everywhere in the reports: a stored zero is shown as `0`, a missing figure as `null`.

## Tests

```bash
dotnet test --filter "Category!=Integration"   # 150 unit tests, no Docker needed
dotnet test                                     # all 211, Docker must be running
```

211 tests. 150 unit tests cover the controllers, the API key handler, validation attributes and the SQL error translator, with repositories replaced by Moq. 61 integration tests start a SQL Server 2022 container, run every script in `database/`, and check the procedures and endpoints against their own test rows (fresh ship codes per test class), so they exercise the real SQL rather than a fake.

CI runs both groups separately, then builds the Docker image.

## Known gaps

- No concurrency control. There are no update endpoints today; if one is added it needs a rowversion or ETag so two writers cannot silently overwrite each other.
- Financial reports are read only. There is no budget import and no sign on or sign off endpoint.
- No caching (see above) or rate limiting.
- Crew search and deep paging have known limits at large volume (see Scalability).

## Notes

`System.Security.Cryptography.Pkcs` is pinned to 10.0.14 because `Microsoft.Data.SqlClient` 7.1.1 resolves 9.0.18, which has a high and a moderate advisory (GHSA-846f-f764-76mx, GHSA-2fmv-rv2c-59x7). With the pin, `dotnet list package --vulnerable --include-transitive` reports nothing for either project.
