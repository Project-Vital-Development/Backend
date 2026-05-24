using HotChocolate.Types;
using ProjectVital.Application.DTOs;

namespace ProjectVital.API.GraphQL.Types;

/// <summary>
/// Define o tipo GraphQL "CheckIn" a partir de CheckInDto.
///
/// SCHEMA GERADO:
///   type CheckIn {
///     id: ID!
///     studentId: ID!
///     timestamp: DateTime!
///     durationMinutes: Int   # nullable — duração pode não ser registrada
///   }
///
/// FONTE DE DADOS: CheckInDto (Application/DTOs/CheckInDto.cs)
///   Populado por: CheckInService.ListByStudentAsync() e RegisterAsync()
///
/// IMPORTÂNCIA PARA O ML:
///   Cada CheckIn é um ponto de dado para o modelo de churn.
///   As features calculadas em ml-service/features/engineering.py dependem de:
///   - Timestamp: frequência, dias desde último check-in, tendência temporal
///   - DurationMinutes: (não usado nas features atuais, mas disponível para expansão)
///
/// SE ADICIONAR DurationMinutes ÀS FEATURES DO ML:
///   1. Atualizar engineering.py (adicionar a feature)
///   2. Retreinar o modelo (OBRIGATÓRIO — o vetor de features muda)
///   3. A interface aqui não precisa de mudança (campo já está exposto)
///
/// TIMESTAMP É SEMPRE UTC:
///   CheckInService.RegisterAsync usa DateTime.UtcNow.
///   O frontend deve converter para o timezone local antes de exibir.
/// </summary>
public class CheckInType : ObjectType<CheckInDto>
{
    protected override void Configure(IObjectTypeDescriptor<CheckInDto> descriptor)
    {
        descriptor.Name("CheckIn");

        descriptor.Field(c => c.Id).Type<NonNullType<IdType>>();
        descriptor.Field(c => c.StudentId).Type<NonNullType<IdType>>();

        // Sempre UTC — frontend converte para timezone do usuário
        descriptor.Field(c => c.Timestamp).Type<NonNullType<DateTimeType>>();

        // Nullable: nem todos os sistemas de registro capturam duração
        descriptor.Field(c => c.DurationMinutes).Type<IntType>();
    }
}
