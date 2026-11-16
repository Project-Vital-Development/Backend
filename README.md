# Project Vital — Backend

API GraphQL do sistema de churn prediction para academias de ginástica. Este serviço é o hub central do Project Vital: autentica gestores, expõe todos os dados via schema GraphQL, aplica tenant isolation por academia e orquestra chamadas ao serviço de Machine Learning para geração de predições.

---

## Tecnologias

| Tecnologia | Versão | Finalidade |
|---|---|---|
| ASP.NET Core | .NET 8 LTS | Web framework e middleware pipeline |
| Hot Chocolate | 13+ | GraphQL server |
| Entity Framework Core | 8+ | ORM Code-First para MySQL |
| MySQL | 8.x | Banco de dados relacional |
| BCrypt.Net | latest | Hash de senhas |
| JWT Bearer | latest | Autenticação stateless |
| xUnit + Moq | latest | Testes unitários |
| WireMock.NET | latest | Testes de integração (mock do ML Service) |

---

## Pré-requisitos

| Ferramenta | Versão mínima | Verificar com |
|---|---|---|
| .NET SDK | 8.0 | `dotnet --version` |
| MySQL | 8.0 | `mysql --version` |

---

## Setup Local

```bash
# Configurar variáveis de ambiente
export DB_CONNECTION_STRING="Server=localhost;Database=projectvital;Uid=root;Pwd=senha;"
export JWT_SECRET="sua-chave-super-secreta-minimo-32-caracteres"
export ML_SERVICE_URL="http://localhost:8000"

# Aplicar migrations e criar schema
dotnet ef database update \
  --project ProjectVital.Infrastructure \
  --startup-project ProjectVital.API

# Rodar a API
dotnet run --project ProjectVital.API
# Disponivel em: http://localhost:5000/graphql
```

> Em `ASPNETCORE_ENVIRONMENT=Development`, o backend executa seed automático com 15 alunos distribuídos em 5 perfis de risco. Credenciais: `gestor@vital.dev` / `Senha@123`.

---

## Variáveis de Ambiente

| Variável | Obrigatória | Padrão | Descrição |
|---|---|---|---|
| `DB_CONNECTION_STRING` | Sim | — | Connection string completa para MySQL |
| `JWT_SECRET` | Sim | — | Chave de assinatura JWT (mínimo 32 caracteres) |
| `ML_SERVICE_URL` | Sim | — | URL base do FastAPI ML Service |
| `JWT_EXPIRY_HOURS` | Não | `24` | Validade do token em horas |
| `ML_SERVICE_TIMEOUT_SECONDS` | Não | `30` | Timeout das chamadas HTTP ao ML Service |
| `ASPNETCORE_ENVIRONMENT` | Não | `Production` | `Development` ativa seed automático de dados |

---

## Estrutura do Projeto (Clean Architecture)

```
ProjectVital.API/              # Entry point — DI, middleware, GraphQL config
ProjectVital.Application/     # Use cases, services, DTOs, interfaces
ProjectVital.Domain/           # Entidades, enums, exceções (sem dependências externas)
ProjectVital.Infrastructure/   # EF Core, MySQL, repositórios concretos
ProjectVital.ML/               # Cliente HTTP para o ML Service (FastAPI)
ProjectVital.Tests/            # Testes unitários (xUnit + Moq, sem banco)
ProjectVital.IntegrationTests/ # Testes de integração (WebApplicationFactory + WireMock)
```

---

## Schema GraphQL

### Queries

```graphql
students(academyId: ID!): [Student!]!
student(id: ID!): Student
checkIns(studentId: ID!, from: DateTime, to: DateTime): [CheckIn!]!
predictions(academyId: ID!): [Prediction!]!
prediction(studentId: ID!): Prediction
academyKPIs(academyId: ID!): AcademyKPIs!
plans(academyId: ID!): [Plan!]!
```

### Mutations

```graphql
login(email: String!, password: String!): AuthPayload!
registerStudent(input: RegisterStudentInput!): Student!
updateStudent(id: ID!, input: UpdateStudentInput!): Student!
deleteStudent(id: ID!): Boolean!
checkIn(studentId: ID!): CheckIn!
runPrediction(studentId: ID!): Prediction!
runBatchPrediction(academyId: ID!): [Prediction!]!
retrainModel(academyId: ID!): RetrainResult!
```

---

## Segurança

- **JWT Bearer** obrigatório em todas as queries e mutations, exceto `login`
- Toda query de dados é **scoped ao `academyId`** extraído do token (tenant isolation — nenhum gestor acessa dados de outra academia)
- **Rate limiting:** 100 requisições por minuto por IP
- **GraphQL Introspection** desabilitado em `Production`
- Senhas armazenadas com **BCrypt** (work factor 12)
- `JWT_SECRET` deve ser fornecido exclusivamente via variável de ambiente — nunca hardcoded

---

## Testes

```bash
# Testes unitários (sem banco, sem rede — Moq para dependências)
dotnet test ProjectVital.Tests/

# Testes de integração (requer MySQL acessível — WireMock para ML Service)
dotnet test ProjectVital.IntegrationTests/
```

---

## Migrations EF Core

```bash
# Criar nova migration após alterar entidades
dotnet ef migrations add <NomeDescritivo> \
  --project ProjectVital.Infrastructure \
  --startup-project ProjectVital.API

# Aplicar migrations pendentes
dotnet ef database update

# Listar estado atual das migrations
dotnet ef migrations list
```

> Nunca edite um arquivo de migration já aplicado. Sempre crie uma nova migration para corrigir o schema.

---

## Como o Backend Orquestra o ML Service

1. Mutation `runPrediction(studentId)` ou `runBatchPrediction(academyId)` é recebida
2. `PredictionService` chama `IMLServiceClient.PredictAsync()` ou `PredictBatchAsync()`
3. `MLServiceClient` faz `POST http://ml-service:8000/predict` com `{ student_id, academy_id }`
4. Resposta `{ probability, risk_level, model_version }` é recebida
5. Predição é persistida no MySQL pelo backend via `PredictionRepository`
6. O resultado é retornado ao resolver GraphQL

Se o ML Service estiver indisponível, a mutation retorna erro com código `ML_SERVICE_UNAVAILABLE`. O frontend é responsável por exibir o estado de erro adequado.

---

## Deploy com Docker

```bash
# Build e execução via Docker Compose (a partir da raiz do repositório)
docker compose up --build backend
# API disponivel em: http://localhost:5000/graphql
# Health check: http://localhost:5000/health
```

O `Dockerfile` usa multi-stage build (SDK para compilar, Runtime para servir), minimizando o tamanho da imagem final.
