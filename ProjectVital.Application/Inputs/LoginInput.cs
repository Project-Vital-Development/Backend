using System.ComponentModel.DataAnnotations;

namespace ProjectVital.Application.Inputs;

/// <summary>
/// Input da mutation "login" — credenciais do usuário.
///
/// VALIDADO POR: Hot Chocolate automaticamente antes de chegar ao resolver.
///   Se inválido (email mal formatado, senha muito curta), retorna erro de validação
///   com code "VALIDATION" antes de executar qualquer lógica de negócio.
///
/// USADO EM: AuthResolver.LoginAsync() → AuthService.LoginAsync()
///
/// EMAIL:
///   [EmailAddress] valida o formato (contém @, domínio válido).
///   Não valida se o domínio existe — isso é responsabilidade de um serviço externo.
///   Case-insensitive na busca (AuthService compara com ToLower ou banco normaliza).
///
/// PASSWORD:
///   Mínimo 8 caracteres — regra de segurança básica.
///   Não é validada complexidade (maiúsculas, símbolos) nesta versão.
///   SE ADICIONAR VALIDAÇÃO DE COMPLEXIDADE: adicionar [RegularExpression] aqui.
///
/// NOTA DE SEGURANÇA:
///   Mesmo que Email ou Password falhem na validação, a mensagem de erro NÃO deve
///   revelar qual campo está incorreto (user enumeration).
///   O AppErrorFilter já garante mensagem genérica para UnauthorizedAccessException.
/// </summary>
public record LoginInput(
    [Required][EmailAddress][StringLength(255)] string Email,
    [Required][StringLength(255, MinimumLength = 8)] string Password
);
