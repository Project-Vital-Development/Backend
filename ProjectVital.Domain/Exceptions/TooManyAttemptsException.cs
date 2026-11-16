namespace ProjectVital.Domain.Exceptions;

/// <summary>Limite de tentativas excedido (ex.: login com senha errada repetidas vezes).</summary>
public sealed class TooManyAttemptsException : Exception
{
    public TooManyAttemptsException(string message) : base(message) { }
}
