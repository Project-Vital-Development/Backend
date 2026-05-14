using HotChocolate;
using ProjectVital.Domain.Exceptions;

namespace ProjectVital.API.GraphQL.Errors;

/// <summary>
/// Filtro global de erros do Hot Chocolate.
/// Mapeia exceções de domínio para códigos GraphQL legíveis.
/// Sempre remove a stack trace para não vazar detalhes em produção.
/// </summary>
public class AppErrorFilter : IErrorFilter
{
    public IError OnError(IError error)
    {
        var mapped = error.Exception switch
        {
            ForbiddenException => error
                .WithCode("FORBIDDEN")
                .WithMessage(error.Exception.Message),

            UnauthorizedAccessException => error
                .WithCode("AUTH_FAILED")
                .WithMessage(error.Exception.Message),

            NotFoundException => error
                .WithCode("NOT_FOUND")
                .WithMessage(error.Exception.Message),

            KeyNotFoundException => error
                .WithCode("NOT_FOUND")
                .WithMessage(error.Exception.Message),

            BusinessRuleException => error
                .WithCode("BUSINESS_RULE_VIOLATION")
                .WithMessage(error.Exception.Message),

            InvalidOperationException => error
                .WithCode("BUSINESS_RULE_VIOLATION")
                .WithMessage(error.Exception.Message),

            NotImplementedException => error
                .WithCode("NOT_IMPLEMENTED")
                .WithMessage(error.Exception.Message),

            _ => error
        };

        // Remove stack trace — nunca expor detalhes internos ao cliente
        return mapped.RemoveException();
    }
}
