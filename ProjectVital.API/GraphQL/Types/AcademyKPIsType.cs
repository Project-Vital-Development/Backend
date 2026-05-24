using HotChocolate.Types;
using ProjectVital.Application.DTOs;

namespace ProjectVital.API.GraphQL.Types;

/// <summary>
/// Define o tipo GraphQL "AcademyKPIs" a partir de AcademyKPIsDto.
///
/// SCHEMA GERADO:
///   type AcademyKPIs {
///     academyId: ID!
///     totalStudents: Int!
///     activeStudents: Int!
///     highRiskCount: Int!
///     mediumRiskCount: Int!
///     lowRiskCount: Int!
///     checkInsThisWeek: Int!
///     averageWeeklyFrequency: Float!
///     generatedAt: DateTime!
///   }
///
/// FONTE DE DADOS: AcademyKPIsDto (Application/DTOs/AcademyKPIsDto.cs)
///   Calculado por: AcademyService.GetKPIsAsync()
///   Chamado por: KPIResolver.AcademyKPIsAsync()
///
/// COMO OS VALORES SÃO CALCULADOS (AcademyService.GetKPIsAsync):
///   totalStudents, activeStudents → StudentRepository.CountByAcademyAsync()
///   highRiskCount, mediumRiskCount, lowRiskCount → PredictionRepository.CountByRiskLevelAsync()
///   checkInsThisWeek → CheckInRepository.CountByAcademyInPeriodAsync (últimos 7 dias)
///   averageWeeklyFrequency → checkInsThisWeek / activeStudents (0.0 se não há ativos)
///   generatedAt → DateTime.UtcNow no momento do cálculo
///
/// NOTA: alunos ativos SEM predição não entram nos contadores de risco.
///   O frontend pode calcular "noRiskDataCount" como:
///   activeStudents - highRiskCount - mediumRiskCount - lowRiskCount
///
/// SE ADICIONAR NOVO KPI:
///   1. Adicionar propriedade em AcademyKPIsDto.cs
///   2. Calcular em AcademyService.GetKPIsAsync()
///   3. Adicionar descriptor.Field aqui
///   4. Adicionar ao frontend (Apollo query + card no Dashboard)
///
/// USO NO FRONTEND:
///   Dashboard principal — atualizar periodicamente com Apollo refetch ou polling.
///   Exibir cards: Total / Ativos / Alto Risco / Médio Risco / Baixo Risco
/// </summary>
public class AcademyKPIsType : ObjectType<AcademyKPIsDto>
{
    protected override void Configure(IObjectTypeDescriptor<AcademyKPIsDto> descriptor)
    {
        descriptor.Name("AcademyKPIs");

        // ID da academia — permite o frontend saber a qual academia os KPIs pertencem
        descriptor.Field(k => k.AcademyId).Type<NonNullType<IdType>>();

        // Contagens gerais
        descriptor.Field(k => k.TotalStudents).Type<NonNullType<IntType>>();
        descriptor.Field(k => k.ActiveStudents).Type<NonNullType<IntType>>();

        // Distribuição de risco baseada na última predição de cada aluno ativo
        descriptor.Field(k => k.HighRiskCount).Type<NonNullType<IntType>>();
        descriptor.Field(k => k.MediumRiskCount).Type<NonNullType<IntType>>();
        descriptor.Field(k => k.LowRiskCount).Type<NonNullType<IntType>>();

        // Frequência operacional
        descriptor.Field(k => k.CheckInsThisWeek).Type<NonNullType<IntType>>();
        descriptor.Field(k => k.AverageWeeklyFrequency).Type<NonNullType<FloatType>>();

        // Timestamp do cálculo — útil para mostrar "atualizado às HH:mm" no frontend
        descriptor.Field(k => k.GeneratedAt).Type<NonNullType<DateTimeType>>();
    }
}
