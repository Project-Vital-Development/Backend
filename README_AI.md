# README_AI.md — Backend Layer (ASP.NET Core + Hot Chocolate GraphQL + EF Core)

> **Para IAs:** Leia o `CLAUDE.md` na raiz antes deste arquivo.
> Esta camada é o hub central do sistema. Toda lógica de negócio, autenticação e orquestração ocorre aqui.

---

## Layer Purpose

O backend é o único ponto de acesso ao banco de dados e ao serviço de ML para o frontend. Implementa a API GraphQL como contrato único, gerencia autenticação JWT, e orquestra chamadas ao MySQL (via EF Core) e ao Python ML Service (via HTTP).

**É o ÚNICO componente que:**
- Valida JWTs
- Escreve no MySQL
- Chama o serviço Python/ML
- Define o schema GraphQL (fonte da verdade para o frontend)

---

## Responsibilities

1. Expor schema GraphQL como contrato de API
2. Autenticar usuários (email/password → JWT)
3. Autorizar requests por role (Admin, Manager)
4. CRUD de entidades: Academy, Student, Plan, CheckIn, Prediction
5. Orquestrar chamada ao ML Service para gerar predições
6. Persistir predições retornadas pelo ML Service no MySQL
7. Calcular KPIs agregados para o dashboard

---

## Internal Architecture

### Estrutura de Projetos (Clean Architecture)

```
backend/
├── ProjectVital.sln
├── ProjectVital.API/              # Entry point
│   ├── Program.cs                 # DI container, middleware, GraphQL config
│   ├── appsettings.json
│   ├── appsettings.Development.json
│   └── Schema/
│       ├── Query.cs               # Root query type Hot Chocolate
│       ├── Mutation.cs            # Root mutation type Hot Chocolate
│       └── Types/                 # GraphQL object types (StudentType, etc.)
│
├── ProjectVital.Application/      # Use cases e contratos
│   ├── Services/
│   │   ├── AuthService.cs         # Login, JWT generation
│   │   ├── StudentService.cs
│   │   ├── CheckInService.cs
│   │   └── PredictionService.cs   # Orquestra chamada ao ML + persistência
│   ├── DTOs/
│   │   ├── LoginInput.cs
│   │   ├── RegisterStudentInput.cs
│   │   └── PredictionResult.cs
│   └── Interfaces/
│       ├── IStudentRepository.cs
│       ├── ICheckInRepository.cs
│       └── IMLServiceClient.cs
│
├── ProjectVital.Domain/           # Domínio puro — sem dependências externas
│   ├── Entities/
│   │   ├── User.cs
│   │   ├── Academy.cs
│   │   ├── Student.cs
│   │   ├── Plan.cs
│   │   ├── CheckIn.cs
│   │   └── Prediction.cs
│   └── Enums/
│       ├── UserRole.cs            # Admin, Manager, Student
│       └── RiskLevel.cs           # Low, Medium, High
│
├── ProjectVital.Infrastructure/   # Implementação concreta
│   ├── Data/
│   │   ├── AppDbContext.cs        # EF Core DbContext
│   │   └── Migrations/           # EF Core migrations (NUNCA editar manualmente)
│   └── Repositories/
│       ├── StudentRepository.cs
│       ├── CheckInRepository.cs
│       └── PredictionRepository.cs
│
└── ProjectVital.ML/               # Cliente HTTP para o Python ML Service
    └── MLServiceClient.cs         # Implementa IMLServiceClient
```

### Pipeline de Middleware (Program.cs)
```csharp
// Ordem é CRÍTICA — não reordenar sem análise
app.UseHttpsRedirection();
app.UseCors("AllowFrontend");        // CORS antes de auth
app.UseAuthentication();             // Valida JWT
app.UseAuthorization();              // Aplica políticas de role
app.MapGraphQL();                    // Endpoint /graphql
app.MapGet("/health", ...);          // Health check
```

### Configuração JWT (Program.cs)
```csharp
builder.Services
  .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
  .AddJwtBearer(options => {
    options.TokenValidationParameters = new TokenValidationParameters {
      ValidateIssuerSigningKey = true,
      IssuerSigningKey = new SymmetricSecurityKey(
        Encoding.ASCII.GetBytes(builder.Configuration["JWT_SECRET"])
      ),
      ValidateIssuer = false,
      ValidateAudience = false,
      ClockSkew = TimeSpan.Zero    // Expiração exata sem tolerância
    };
  });
```

### Geração de JWT (AuthService.cs)
```csharp
// Claims obrigatórios no token:
// - sub: userId (string)
// - role: "Admin" | "Manager"
// - academyId: academyId (string) — null para Admin global
// - exp: DateTime.UtcNow + TimeSpan.FromHours(JWT_EXPIRY_HOURS)
```

---

## External Dependencies (NuGet Packages)

| Package | Finalidade |
|---|---|
| `HotChocolate.AspNetCore` | GraphQL server |
| `HotChocolate.Data` | Filtering, sorting, pagination |
| `Pomelo.EntityFrameworkCore.MySql` | EF Core provider para MySQL |
| `Microsoft.EntityFrameworkCore.Design` | Migrations tooling |
| `Microsoft.AspNetCore.Authentication.JwtBearer` | JWT validation middleware |
| `BCrypt.Net-Next` | Hash de senhas |
| `System.IdentityModel.Tokens.Jwt` | Geração de JWT |

---

## Communication Contracts

### Recebe de: Frontend (GraphQL)
- Endpoint: `POST /graphql`
- Auth: `Authorization: Bearer <JWT>` no header
- Schema: definido pelos types Hot Chocolate em `Schema/`

### Envia para: MySQL (EF Core)
- CRUD completo via repositórios
- Migrations gerenciam schema

### Envia para: Python ML Service (HTTP)
- `POST {ML_SERVICE_URL}/predict`
- `POST {ML_SERVICE_URL}/predict/batch`
- `POST {ML_SERVICE_URL}/retrain`
- Timeout: 30 segundos (configurável)
- Retry: 2 tentativas

---

## Folder Structure

Ver seção Internal Architecture acima.

---

## Important Files

| Arquivo | Criticidade | Descrição |
|---|---|---|
| `Program.cs` | CRÍTICO | Toda configuração da aplicação. DI, middleware, GraphQL. |
| `AppDbContext.cs` | CRÍTICO | Mapeamento EF Core → MySQL. Modificar com cuidado. |
| `AuthService.cs` | CRÍTICO | Geração e validação de JWT. Senhas BCrypt. |
| `MLServiceClient.cs` | ALTA | HTTP client para Python. Falha aqui = sem predições. |
| `appsettings.json` | CRÍTICO | NUNCA commitar com valores reais de prod. |
| `Migrations/` | ALTA | NUNCA editar arquivos existentes de migration. |

---

## Lifecycle

### Startup (Program.cs)
```
1. Carregar configurações (appsettings + env vars)
2. Registrar serviços no DI container
3. Configurar EF Core + MySQL connection pool
4. Configurar Hot Chocolate GraphQL schema
5. Configurar JWT authentication
6. Configurar CORS (apenas origins explícitas em prod)
7. Build app + run
```

### Request lifecycle (Query GraphQL)
```
1. HTTP POST /graphql com Authorization header
2. JWT Middleware: valida token, popula HttpContext.User
3. Hot Chocolate: resolve fields via Resolver
4. Resolver: injeta Service via DI
5. Service: executa lógica de negócio
6. Repository: executa EF Core query → MySQL
7. Dados retornam pela cadeia
8. Hot Chocolate serializa resposta JSON GraphQL
```

### Lifecycle de predição
```
1. Mutation runPrediction(studentId) recebida
2. PredictionService.RunAsync(studentId)
3. MLServiceClient.PredictAsync(studentId, academyId)
4. HTTP POST para Python ML Service
5. Resposta: { probability, risk_level, model_version }
6. PredictionRepository.SaveAsync(new Prediction { ... })
7. Retorna Prediction para o resolver GraphQL
```

---

## Security Concerns

- **JWT_SECRET**: NUNCA hardcoded no código — sempre via variável de ambiente
- **BCrypt work factor**: usar mínimo 12 (padrão 10 é insuficiente para produção)
- **CORS**: `AllowedOrigins` explícito em produção — nunca `AllowAnyOrigin()`
- **GraphQL Introspection**: desabilitar em produção
  ```csharp
  // Em produção:
  options.EnableSchemaIntrospection = false;
  ```
- **SQL Injection**: mitigado pelo EF Core — nunca usar interpolação de string em queries raw
- **Mass assignment**: validar explicitamente campos em DTOs de input — não usar `AutoMapper` sem whitelist

---

## Performance Concerns

- **N+1 Queries**: sem DataLoader, uma query de lista de students com campo `checkins` dispara N queries extras
  - Solução: implementar `IDataLoader<StudentId, CheckIn[]>` via Hot Chocolate
- **Connection Pool**: EF Core gerencia pool automático — não criar DbContext manualmente fora do DI
- **Async/Await**: todos os resolvers e services devem ser async — nunca `.Result` ou `.Wait()`

---

## AI Modification Rules

1. **Ao adicionar campo ao schema GraphQL**: adicionar Type + Resolver + Service method + Repository method (nesta ordem)
2. **Ao adicionar entidade**: criar classe em Domain/Entities → adicionar ao DbContext → criar migration
3. **Ao alterar entidade**: criar nova migration (NUNCA editar migration existente)
4. **Ao alterar AuthService**: testar login completo end-to-end
5. **Ao alterar MLServiceClient**: verificar contrato HTTP com o serviço Python (seção 11 do CLAUDE.md)
6. **NUNCA** usar `context.Students.ToList()` sem filtro — sempre filtrar por `AcademyId` (multitenancy)

---

## Testing Strategy

- NÃO IDENTIFICADO: estratégia de testes definida
- Recomendado: xUnit + Moq para unit tests dos Services
- Recomendado: WebApplicationFactory para integration tests do GraphQL endpoint

---

## Deployment Notes

- **Dev:** `dotnet run --project ProjectVital.API` → porta 5000
- **Docker:** Multi-stage build (SDK → Runtime)
- **Migrations em prod:** Executar `dotnet ef database update` antes de subir nova versão
- **Health check:** `GET /health` deve retornar 200 para o Docker health check

---

## Known Risks

1. Sem retry/circuit breaker formal para chamadas ao ML Service
2. JWT sem refresh token — sessão expira e não é renovável automaticamente
3. Multitenancy por `AcademyId` no código — risco de data leak se filtro for esquecido em nova query

---

## Technical Debt

- TD-03: DataLoader não implementado (N+1 queries)
- TD-05: Sem soft delete (LGPD)
- TD-08: GraphQL introspection em produção
- TD-09: Sem rate limiting
- TD-10: ML client sem versionamento de contrato

---

## Recovery Notes

Se o backend não responder:
```bash
dotnet run --project ProjectVital.API
# Verificar: porta 5000 livre?
# Verificar: variáveis de ambiente configuradas?
# Verificar: MySQL acessível?
curl http://localhost:5000/health
```

Se migrations falham:
```bash
dotnet ef migrations list                    # Ver estado das migrations
dotnet ef database update <MigrationName>    # Aplicar até migration específica
```

---

## Update Instructions

Ao implementar features no backend:
1. Registrar no Change Log do CLAUDE.md
2. Se nova entidade: criar migration + atualizar AppDbContext
3. Se novo campo GraphQL: atualizar Type + adicionar ao schema
4. Se mudança no contrato HTTP com ML Service: atualizar seção 11 do CLAUDE.md
