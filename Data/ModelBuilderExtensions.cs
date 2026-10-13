using Microsoft.EntityFrameworkCore;

namespace Aucy.Data;

internal static class ModelBuilderExtensions
{
    public static void HasCheckConstraint<TEntity>(this ModelBuilder builder, string fieldName, string constraint)
        where TEntity : class
    {
        builder.Entity<TEntity>()
            .ToTable(t => t.HasCheckConstraint($"CK_{nameof(TEntity)}_{fieldName}", $"\"{fieldName}\" {constraint}"));
    }
}