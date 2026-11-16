using HotChocolate;
using ProjectVital.Domain.Exceptions;

namespace ProjectVital.API.GraphQL.Errors;

/// <summary>
/// Converte exceções em erros GraphQL com códigos estáveis.
///
/// SEGURANÇA: só exceções de DOMÍNIO (mensagens escritas por nós) têm a mensagem
/// repassada ao cliente. Qualquer outra exceção (EF Core, MySQL, HTTP, bugs) vira
/// "Erro interno." — a mensagem original pode conter SQL, nomes de tabelas ou dados,
/// e fica apenas no log do servidor, com o id de correlação da requisição.
/// </summary>
public class AppErrorFilter(ILogger<AppErrorFilter> logger) : IErrorFilter
{
    public IError OnError(IError error)
    {
        var ex = error.Exception;
        if (ex is null)
            return error; // erros de validação/parse do próprio GraphQL (sem exceção)

        var mapped = ex switch
        {
            ForbiddenException => error.WithCode("FORBIDDEN").WithMessage(ex.Message),
            UnauthorizedAccessException => error.WithCode("AUTH_FAILED").WithMessage(ex.Message),
            NotFoundException => error.WithCode("NOT_FOUND").WithMessage(ex.Message),
            BusinessRuleException => error.WithCode("BUSINESS_RULE_VIOLATION").WithMessage(ex.Message),
            TooManyAttemptsException => error.WithCode("TOO_MANY_ATTEMPTS").WithMessage(ex.Message),
            ServiceUnavailableException => error.WithCode("SERVICE_UNAVAILABLE")
                .WithMessage("Serviço de predição indisponível no momento. Tente novamente em instantes."),
            _ => error.WithCode("INTERNAL_ERROR").WithMessage("Erro interno."),
        };

        if (ex is ServiceUnavailableException)
            logger.LogWarning("ML Service indisponível: {Message} ({Inner})", ex.Message, ex.InnerException?.GetType().Name);
        else if (ex is not DomainException and not UnauthorizedAccessException and not TooManyAttemptsException)
            logger.LogError(ex, "Erro não tratado no resolver {Path}", error.Path?.ToString());

        return mapped.RemoveException().RemoveExtension("stackTrace");
    }
}
