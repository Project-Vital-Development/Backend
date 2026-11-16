namespace ProjectVital.Domain.Exceptions;

/// <summary>
/// Exceção base abstrata para erros de domínio do Project Vital.
/// Todas as exceções específicas da lógica de negócio herdam desta.
///
/// POR QUE EXISTE: permite que AppErrorFilter.cs capture qualquer exceção
/// de domínio com um único catch e mapeie para um código GraphQL legível.
///
/// HIERARQUIA:
///   DomainException (base abstrata)
///   ├── NotFoundException      → código GraphQL "NOT_FOUND"
///   ├── ForbiddenException     → código GraphQL "FORBIDDEN"
///   └── BusinessRuleException  → código GraphQL "BUSINESS_RULE_VIOLATION"
///
/// COMO O GRAPHQL RECEBE: AppErrorFilter.OnError() intercepta estas exceções
/// e as converte em respostas GraphQL estruturadas com "code" e "message",
/// removendo a stack trace para não vazar detalhes internos ao cliente.
///
/// QUANDO CRIAR UMA NOVA SUBCLASSE:
///   1. Herdar de DomainException
///   2. Adicionar o novo tipo no switch do AppErrorFilter.cs
///   3. Definir o código GraphQL correspondente
/// </summary>
public abstract class DomainException : Exception
{
    protected DomainException(string message) : base(message) { }
}
