using HotChocolate;
using ProjectVital.Domain.Exceptions;

namespace ProjectVital.API.GraphQL.Errors;

/// <summary>
/// Filtro global de erros do Hot Chocolate.
/// Intercepta toda exceção não tratada lançada por qualquer resolver e a converte
/// em um erro GraphQL com código legível, sem expor stack traces ao cliente.
///
/// REGISTRADO EM: Program.cs → .AddErrorFilter<AppErrorFilter>()
///
/// COMO FUNCIONA:
///   Hot Chocolate captura qualquer exceção lançada dentro de um resolver e chama
///   OnError() antes de serializar a resposta. Aqui mapeamos exceções de domínio
///   para códigos padronizados que o frontend pode usar para decidir o que mostrar.
///
/// FORMATO DE SAÍDA (resposta GraphQL ao cliente):
///   {
///     "errors": [{
///       "message": "Mensagem amigável",
///       "extensions": { "code": "FORBIDDEN" }
///     }]
///   }
///
/// POR QUE RemoveException():
///   Por padrão o Hot Chocolate inclui o tipo da exceção e a stack trace na resposta.
///   Isso vaza detalhes de implementação interna. RemoveException() garante que
///   apenas message e code chegam ao cliente, em qualquer ambiente.
///
/// SE ADICIONAR NOVA EXCEÇÃO DE DOMÍNIO:
///   1. Criar a classe em ProjectVital.Domain/Exceptions/ (herdar de DomainException)
///   2. Adicionar case no switch abaixo com o código GraphQL desejado
///   3. Atualizar o frontend para tratar o novo código
///
/// MAPEAMENTOS ATUAIS:
///   ForbiddenException          → "FORBIDDEN"          (autenticado, sem permissão)
///   UnauthorizedAccessException → "AUTH_FAILED"         (credenciais inválidas)
///   NotFoundException           → "NOT_FOUND"           (entidade não existe)
///   KeyNotFoundException        → "NOT_FOUND"           (dicionário/coleção .NET)
///   BusinessRuleException       → "BUSINESS_RULE_VIOLATION" (regra de negócio)
///   InvalidOperationException   → "BUSINESS_RULE_VIOLATION" (operação inválida)
///   NotImplementedException     → "NOT_IMPLEMENTED"     (placeholder de fase futura)
///   (qualquer outra)            → mantém o erro original do Hot Chocolate
/// </summary>
public class AppErrorFilter : IErrorFilter
{
    /// <summary>
    /// Chamado pelo Hot Chocolate para cada erro gerado durante a execução de um resolver.
    /// Retorna o erro original (possivelmente enriquecido com code e message) sem stack trace.
    ///
    /// SE MUDAR UMA EXCEÇÃO:
    ///   - Renomear ou remover uma exceção de domínio → atualizar o case correspondente aqui
    ///   - Mudar o código GraphQL → atualizar o frontend que faz switch no error.extensions.code
    ///
    /// NOTA: o caso `_ => error` (fallback) mantém o comportamento padrão do HC para
    /// exceções não mapeadas — útil para erros de validação do schema que o HC já formata.
    /// </summary>
    public IError OnError(IError error)
    {
        var mapped = error.Exception switch
        {
            // Autenticado, mas sem permissão para o recurso (ex: Manager tentando
            // acessar academia de outro Manager). Lançado por ResolverAuthHelper.
            ForbiddenException => error
                .WithCode("FORBIDDEN")
                .WithMessage(error.Exception.Message),

            // Credenciais inválidas no login. Lançado por AuthService.LoginAsync.
            // Mensagem genérica "Credenciais inválidas." — não revela se o email existe.
            UnauthorizedAccessException => error
                .WithCode("AUTH_FAILED")
                .WithMessage(error.Exception.Message),

            // Entidade não encontrada pelo Id. Lançado por múltiplos resolvers
            // antes de verificar acesso (ex: studentService.GetByIdAsync retorna null).
            NotFoundException => error
                .WithCode("NOT_FOUND")
                .WithMessage(error.Exception.Message),

            // Variante .NET de "chave não encontrada" — tratada igual a NotFoundException.
            KeyNotFoundException => error
                .WithCode("NOT_FOUND")
                .WithMessage(error.Exception.Message),

            // Violação de regra de negócio explícita (ex: CheckIn em aluno inativo).
            // Lançado por CheckInService, StudentService, etc.
            BusinessRuleException => error
                .WithCode("BUSINESS_RULE_VIOLATION")
                .WithMessage(error.Exception.Message),

            // InvalidOperationException também é usada para regras de negócio
            // em serviços que não usam BusinessRuleException diretamente.
            InvalidOperationException => error
                .WithCode("BUSINESS_RULE_VIOLATION")
                .WithMessage(error.Exception.Message),

            // Funcionalidade ainda não implementada (ex: RetrainModelAsync placeholder).
            // Remover este case quando a funcionalidade for implementada.
            NotImplementedException => error
                .WithCode("NOT_IMPLEMENTED")
                .WithMessage(error.Exception.Message),

            // Fallback: qualquer outra exceção mantém o formato padrão do Hot Chocolate.
            _ => error
        };

        // Remove stack trace — nunca expor detalhes internos ao cliente.
        // Isso vale mesmo em Development — o desenvolvedor deve ver o stack trace
        // nos logs do servidor, não na resposta GraphQL.
        return mapped.RemoveException();
    }
}
