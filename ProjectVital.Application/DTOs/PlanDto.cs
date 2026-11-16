namespace ProjectVital.Application.DTOs;

/// <summary>
/// DTO de plano de mensalidade — representa as opções de plano de uma academia.
///
/// CRIADO POR: PlanQueryResolver (consulta direta ao IPlanRepository; não há PlanService)
/// RELACIONAMENTO: cada Student tem um PlanId (FK para Plan)
///
/// MULTITENANT:
///   Planos pertencem a uma academia específica (AcademyId).
///   Um Manager só pode ver e atribuir planos da sua própria academia.
///
/// DURATIONDAYS:
///   Duração do plano em dias (ex: 30=mensal, 90=trimestral, 365=anual).
///   Não usado diretamente pelo ML atualmente — a assiduidade (check-ins) é mais relevante.
///
/// USADO COMO:
///   StudentDto.PlanName: apenas o nome do plano é exibido no StudentDto
///   (o PlanDto completo não é retornado diretamente nas queries atuais)
///
/// SE CRIAR CRUD DE PLANOS:
///   Criar PlanService, PlanResolver e PlanType para expor via GraphQL.
/// </summary>
public record PlanDto(
    int Id,
    // Academia dona deste plano — planos são por tenant
    int AcademyId,
    // Ex: "Mensal", "Trimestral", "Anual"
    string Name,
    // Preço em BRL — usar decimal para evitar erros de ponto flutuante
    decimal Price,
    // Duração em dias: 30=mensal, 90=trimestral, 365=anual
    int DurationDays
);
