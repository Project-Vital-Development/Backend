namespace ProjectVital.Domain.Exceptions;

public sealed class ForbiddenException : DomainException
{
    public ForbiddenException(string message = "Acesso negado: você não tem permissão para este recurso.")
        : base(message) { }
}
