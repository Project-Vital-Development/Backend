namespace ProjectVital.Domain.Exceptions;

/// <summary>
/// Dependência externa (ML Service) fora do ar, lenta ou com resposta inválida.
/// O AppErrorFilter devolve ao cliente apenas uma mensagem genérica (SERVICE_UNAVAILABLE);
/// o detalhe técnico fica na InnerException, registrada no log do servidor.
/// </summary>
public sealed class ServiceUnavailableException : Exception
{
    public ServiceUnavailableException(string message, Exception? inner = null) : base(message, inner) { }
}
