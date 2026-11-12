using Microsoft.EntityFrameworkCore;

namespace Aucy.Data;

internal static class ModelBuilderExtensions
{
    public static void HasCheckConstraint<TEntity>(this ModelBuilder builder, string fieldName, string constraint, string? addConstraintName = null)
        where TEntity : class
    {
        var constraintName = addConstraintName == null ? $"CK_{typeof(TEntity).Name}_{fieldName}" : 
            $"CK_{typeof(TEntity).Name}_{fieldName}_{addConstraintName}";

        builder.Entity<TEntity>()
            .ToTable(t => t.HasCheckConstraint(constraintName, $"\"{fieldName}\" {constraint}"));
    }
}