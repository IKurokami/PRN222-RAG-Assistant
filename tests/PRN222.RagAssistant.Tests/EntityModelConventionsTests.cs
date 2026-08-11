using Microsoft.EntityFrameworkCore;
using PRN222.RagAssistant.Domain.Entities;

namespace PRN222.RagAssistant.Tests;

public sealed class EntityModelConventionsTests
{
    private static readonly Type[] EntityTypes = typeof(Subject).Assembly
        .GetTypes()
        .Where(type =>
            type.IsClass &&
            !type.IsAbstract &&
            type.Namespace == "PRN222.RagAssistant.Domain.Entities")
        .ToArray();

    [Fact]
    public void Entities_must_not_define_navigation_properties()
    {
        var entityTypeSet = EntityTypes.ToHashSet();
        var violations = EntityTypes
            .SelectMany(entityType => entityType
                .GetProperties()
                .Where(property => IsNavigationProperty(property.PropertyType, entityTypeSet))
                .Select(property => $"{entityType.Name}.{property.Name}"))
            .OrderBy(name => name)
            .ToArray();

        Assert.True(
            violations.Length == 0,
            $"Entity navigation properties are not allowed. Use scalar foreign-key IDs and configure relationships in IEntityTypeConfiguration<T>. Violations: {string.Join(", ", violations)}");
    }

    [Fact]
    public void Every_entity_must_have_a_dedicated_configuration_class()
    {
        var configuredEntityTypes = typeof(Subject).Assembly
            .GetTypes()
            .Where(type => !type.IsAbstract && !type.IsInterface)
            .SelectMany(type => type.GetInterfaces())
            .Where(@interface =>
                @interface.IsGenericType &&
                @interface.GetGenericTypeDefinition() == typeof(IEntityTypeConfiguration<>))
            .Select(@interface => @interface.GetGenericArguments()[0])
            .ToHashSet();

        var missingConfigurations = EntityTypes
            .Where(entityType => !configuredEntityTypes.Contains(entityType))
            .Select(entityType => entityType.Name)
            .OrderBy(name => name)
            .ToArray();

        Assert.True(
            missingConfigurations.Length == 0,
            $"Every entity requires a dedicated IEntityTypeConfiguration<T>. Missing: {string.Join(", ", missingConfigurations)}");
    }

    private static bool IsNavigationProperty(Type propertyType, HashSet<Type> entityTypes)
    {
        if (entityTypes.Contains(propertyType))
        {
            return true;
        }

        if (propertyType == typeof(string) || !propertyType.IsGenericType)
        {
            return false;
        }

        return propertyType
            .GetGenericArguments()
            .Any(entityTypes.Contains);
    }
}
