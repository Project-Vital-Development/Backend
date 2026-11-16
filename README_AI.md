# README_AI.md — Backend (ASP.NET Core 8 + Hot Chocolate GraphQL + EF Core)

> Referência rápida para IAs e desenvolvedores. Contexto completo: `CLAUDE.md` (seções 6, 8, 9 e 11).
> Atualizado em 2026-11-13.

## Responsabilidade
Único orquestrador: autenticação (JWT), regras de negócio, acesso ao MySQL (EF Core) e chamadas ao ML
Service (HTTP + `X-Internal-Key`). O frontend só fala com este serviço, via `/graphql`.

## Projetos
| Projeto | Conteúdo |
|---|---|
| `ProjectVital.API` | `Program.cs` (DI, JWT, CORS, rate limit, GraphQL), `GraphQL/` (Resolvers, Types, `AppErrorFilter`), `Security/` (tenant, throttles, cabeçalhos, correlation id), `Seed/DemoDataSeeder.cs`, `Background/` (`ModelBootstrapService`, `PredictionRefreshService`) |
| `ProjectVital.Application` | Services, DTOs, Inputs, interfaces (`ITenantContext`, `IMLServiceClient`...) |
| `ProjectVital.Domain` | Entidades, enums (`RiskLevel`, `UserRole`), exceções, interfaces de repositório |
| `ProjectVital.Infrastructure` | `ProjectVitalDbContext` (filtros globais de academia e soft delete), configurações, repositórios, `Migrations/` (3) |
| `ProjectVital.ML` | `MLServiceClient` (DTOs do contrato com o Python) |
| `ProjectVital.Tests` / `ProjectVital.IntegrationTests` | 22 testes unitários / 49 de integração (MySQL + WireMock) |

## Operações GraphQL
- **Queries:** `students`, `student`, `checkIns`, `predictions`, `prediction`, `academyKPIs`, `plans`, `modelReport`
- **Mutations:** `login`, `registerStudent`, `updateStudent`, `deleteStudent` (soft delete/LGPD), `checkIn`, `checkOut`, `runPrediction`, `runBatchPrediction`, `retrainModel`
- **Códigos de erro** (`AppErrorFilter`): `AUTH_FAILED`, `AUTH_NOT_AUTHORIZED` (token ausente/inválido/expirado), `FORBIDDEN`, `NOT_FOUND`, `BUSINESS_RULE_VIOLATION`, `TOO_MANY_ATTEMPTS`, `SERVICE_UNAVAILABLE`, `INTERNAL_ERROR`

## Regras que não podem ser quebradas
1. **Isolamento entre academias**: a academia vem do TOKEN (`ITenantContext`, `ResolverAuthHelper`), nunca de um parâmetro confiado; filtros globais no EF Core.
2. Resolvers de leitura usam `[Service(ServiceKind.Resolver)]` (DbContext próprio — o Hot Chocolate executa campos em paralelo); mutations compartilham o escopo.
3. Schema GraphQL: adicionar campos é seguro; remover ou renomear quebra o frontend (`frontend/src/graphql` e os mocks em `src/tests/mocks/handlers.ts`).
4. Migrations: só novas (`dotnet ef migrations add ...`), nunca editar as existentes; depois regenere `database/migrations/schema_idempotente.sql`.
5. Com `Accept: */*` o Hot Chocolate 13 responde **HTTP 500** quando `data` volta null; o frontend envia `Accept: application/json` (recebe 200 + `errors`). Não remova isso do `apolloClient.ts`.
6. O ML treina só XGBoost; o `ModelBootstrapService` retreina academias sem relatório no formato atual ao subir.

## Rodar e testar
```bash
cd backend
export DOTNET_ROLL_FORWARD=Major          # máquina só com runtime ASP.NET Core 9 (alvo net8)
dotnet build ProjectVital.sln -c Release
dotnet test ProjectVital.Tests            # 22
dotnet test ProjectVital.IntegrationTests # 49 — MySQL do Docker em localhost:3307 (root/rootpassword123)
```
Sem Docker: `scripts/dev_local.ps1` exporta as variáveis (o `.env` da raiz só é lido pelo docker compose).

## Escopo congelado
Refresh token, DataLoader, troca de senha, CRUD de academias e planos, login de alunos: fora do escopo
(ver `CLAUDE.md`, seção 15.1).
