namespace ProjectVital.Domain.Exceptions;

/// <summary>
/// Lançada quando uma operação viola uma regra de negócio do sistema.
/// AppErrorFilter mapeia para código GraphQL "BUSINESS_RULE_VIOLATION".
///
/// ONDE É LANÇADA:
///   - StudentService.RegisterAsync() → email já em uso
///   - StudentService.UpdateAsync()  → novo email já em uso por outro usuário
///   - StudentService.RegisterAsync() → falha ao carregar aluno após criação (erro raro)
///   - StudentService.UpdateAsync()  → falha ao carregar aluno após update (erro raro)
///   - CheckInService.RegisterAsync() → aluno inativo tentando fazer check-in
///     (este último usa InvalidOperationException, também mapeada para "BUSINESS_RULE_VIOLATION")
///
/// REGRA DE USO:
///   - Use BusinessRuleException para violações de regras de negócio ESPERADAS
///     (email duplicado, aluno inativo, etc.) — o cliente deve exibir a mensagem ao usuário.
///   - Não use para erros de infraestrutura (banco offline, timeout) — esses sobem como
///     exceções genéricas e o AppErrorFilter os deixa passar sem mapear.
///
/// RESPOSTA GRAPHQL GERADA:
///   { "errors": [{ "message": "E-mail 'x@y.com' já está em uso.", "extensions": { "code": "BUSINESS_RULE_VIOLATION" } }] }
/// </summary>
public sealed class BusinessRuleException : DomainException
{
    public BusinessRuleException(string message) : base(message) { }
}
