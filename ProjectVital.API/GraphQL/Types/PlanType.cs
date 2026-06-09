using HotChocolate.Types;
using ProjectVital.Domain.Entities;

namespace ProjectVital.API.GraphQL.Types;

/// <summary>
/// Define o tipo GraphQL "Plan" para exposição dos planos de mensalidade.
///
/// SCHEMA GERADO:
///   type Plan {
///     id: ID!
///     academyId: ID!
///     name: String!       # ex: "Mensal", "Trimestral", "Anual"
///     price: Decimal!     # em BRL
///     durationDays: Int!  # 30, 90, 365
///   }
///
/// USADO EM: PlanQueryResolver.PlansAsync() → query "plans(academyId: ID!)"
/// </summary>
public class PlanType : ObjectType<Plan>
{
    protected override void Configure(IObjectTypeDescriptor<Plan> descriptor)
    {
        descriptor.Name("Plan");
        descriptor.Field(p => p.Id).Type<NonNullType<IdType>>();
        descriptor.Field(p => p.AcademyId).Type<NonNullType<IdType>>();
        descriptor.Field(p => p.Name).Type<NonNullType<StringType>>();
        descriptor.Field(p => p.Price).Type<NonNullType<DecimalType>>();
        descriptor.Field(p => p.DurationDays).Type<NonNullType<IntType>>();
    }
}
