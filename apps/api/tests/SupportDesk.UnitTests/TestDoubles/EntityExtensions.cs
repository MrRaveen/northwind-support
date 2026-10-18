using System.Reflection;
using SupportDesk.Domain.Common;

namespace SupportDesk.UnitTests.TestDoubles;

/// <summary>
/// Entity ids are assigned by the database, so they are zero in a unit test. This gives an
/// entity the id a test needs, for example to match an agent's specialization to a category.
/// </summary>
public static class EntityExtensions
{
    public static T WithId<T>(this T entity, int id) where T : Entity
    {
        typeof(Entity)
            .GetProperty(nameof(Entity.Id), BindingFlags.Instance | BindingFlags.Public)!
            .SetValue(entity, id);

        return entity;
    }
}
