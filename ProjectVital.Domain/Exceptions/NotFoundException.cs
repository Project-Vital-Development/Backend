namespace ProjectVital.Domain.Exceptions;

/// <summary>
/// Lançada quando uma entidade buscada por Id não existe no banco.
/// AppErrorFilter mapeia para código GraphQL "NOT_FOUND".
///
/// ONDE É LANÇADA:
///   - StudentService.UpdateAsync()          → Student com Id não encontrado
///   - StudentService.DeleteAsync()          → Student com Id não encontrado
///   - StudentMutationResolver.UpdateStudentAsync() → antes de chamar o service
///   - StudentMutationResolver.DeleteStudentAsync() → antes de chamar o service
///   - CheckInQueryResolver.CheckInsAsync()  → Student não encontrado
///   - PredictionQueryResolver.PredictionAsync() → Student não encontrado
///
/// RESPOSTA GRAPHQL GERADA:
///   { "errors": [{ "message": "Student com Id=42 não encontrado.", "extensions": { "code": "NOT_FOUND" } }] }
/// </summary>
public sealed class NotFoundException : DomainException
{
    /// <summary>
    /// Construtor para busca por Id — gera mensagem padronizada.
    /// Ex: new NotFoundException("Student", 42) → "Student com Id=42 não encontrado."
    /// </summary>
    public NotFoundException(string entityName, int id)
        : base($"{entityName} com Id={id} não encontrado.") { }

    /// <summary>Construtor para mensagem personalizada.</summary>
    public NotFoundException(string message) : base(message) { }
}
