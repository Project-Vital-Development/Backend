using HotChocolate.Types;
using ProjectVital.Application.DTOs;

namespace ProjectVital.API.GraphQL.Types;

/// <summary>
/// Define o tipo GraphQL "Student" a partir de StudentDto.
///
/// COMO FUNCIONA:
///   Hot Chocolate usa esta classe para saber quais campos de StudentDto
///   ficam visíveis no schema GraphQL e com quais tipos GraphQL.
///   Sem esta classe, HC tentaria inferir tudo automaticamente — mas a configuração
///   explícita garante controle total sobre o que é exposto.
///
/// SCHEMA GERADO:
///   type Student {
///     id: ID!
///     userId: ID!
///     academyId: ID!
///     planId: ID!
///     name: String!
///     email: String!
///     enrollmentDate: DateTime!
///     active: Boolean!
///     planName: String        # nullable — plano pode não ter sido carregado
///     latestPrediction: Prediction  # nullable — aluno pode não ter predição ainda
///   }
///
/// FONTE DE DADOS: StudentDto (Application/DTOs/StudentDto.cs)
///   Populado por: StudentService.ListByAcademyAsync() e GetByIdAsync()
///   que incluem Plan e LatestPrediction via StudentRepository.GetWithRelationsAsync()
///
/// SE ADICIONAR CAMPO:
///   1. Adicionar propriedade em StudentDto.cs
///   2. Adicionar descriptor.Field aqui com o tipo GraphQL correto
///   3. Popular o campo no StudentService/StudentRepository
///   4. Atualizar queries Apollo no frontend para incluir o novo campo
///
/// SE REMOVER CAMPO:
///   BREAKING CHANGE — qualquer query frontend que use o campo quebra.
///   Preferir deprecar com [Deprecated("Motivo")] antes de remover.
///
/// TIPOS GRAPHQL USADOS:
///   NonNullType<T>: campo obrigatório (! no schema) — erro se o DTO tiver null
///   IdType: serializa int como string no JSON do GraphQL (padrão da spec)
///   StringType, BooleanType, DateTimeType, FloatType: tipos primitivos
/// </summary>
public class StudentType : ObjectType<StudentDto>
{
    protected override void Configure(IObjectTypeDescriptor<StudentDto> descriptor)
    {
        descriptor.Name("Student");

        // Identificadores — serializados como string (ID GraphQL spec)
        descriptor.Field(s => s.Id).Type<NonNullType<IdType>>();
        descriptor.Field(s => s.UserId).Type<NonNullType<IdType>>();
        descriptor.Field(s => s.AcademyId).Type<NonNullType<IdType>>();
        descriptor.Field(s => s.PlanId).Type<NonNullType<IdType>>();

        // Dados pessoais — cobertos pela LGPD (Art.5, I)
        descriptor.Field(s => s.Name).Type<NonNullType<StringType>>();
        descriptor.Field(s => s.Email).Type<NonNullType<StringType>>();

        descriptor.Field(s => s.EnrollmentDate).Type<NonNullType<DateTimeType>>();

        // Active=false significa churn ocorreu — impacta feature "label" no ML
        descriptor.Field(s => s.Active).Type<NonNullType<BooleanType>>();

        // Nullable: só preenchido quando StudentRepository.GetWithRelationsAsync inclui Plan
        descriptor.Field(s => s.PlanName).Type<StringType>();

        // Nullable: null quando o aluno ainda não foi avaliado pelo ML Service
        // O frontend deve exibir "Sem predição disponível" quando null
        descriptor.Field(s => s.LatestPrediction).Type<PredictionType>();
    }
}
