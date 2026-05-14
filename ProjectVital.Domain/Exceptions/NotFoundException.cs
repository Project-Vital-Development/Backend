namespace ProjectVital.Domain.Exceptions;

public sealed class NotFoundException : DomainException
{
    public NotFoundException(string entityName, int id)
        : base($"{entityName} com Id={id} não encontrado.") { }

    public NotFoundException(string message) : base(message) { }
}
