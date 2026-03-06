using System.Linq.Expressions;
using System.Reflection;
using Daeha.CleanArch.Application.Common.Interfaces;
using Daeha.CleanArch.Domain.Common;
using Daeha.CleanArch.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Daeha.CleanArch.Infrastructure.Persistence;

/// <summary>
/// Clean Architecture EF Core DbContext 기반 클래스.
/// 감사 컬럼 자동 설정, 멀티테넌트 글로벌 필터(ITenantScoped), 소프트 삭제 필터(ISoftDeletable),
/// IUnitOfWork 구현을 제공.
/// </summary>
/// <typeparam name="TContext">하위 DbContext 타입 (CRTP 패턴)</typeparam>
/// <example>
/// <code>
/// public sealed class AppDbContext(DbContextOptions&lt;AppDbContext&gt; options, ICurrentUserService currentUser)
///     : BaseDbContext&lt;AppDbContext&gt;(options, currentUser)
/// {
///     public DbSet&lt;Order&gt; Orders => Set&lt;Order&gt;();
///
///     protected override void OnModelCreating(ModelBuilder modelBuilder)
///     {
///         base.OnModelCreating(modelBuilder); // 반드시 호출
///         modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
///     }
/// }
/// </code>
/// </example>
public abstract class BaseDbContext<TContext>(
    DbContextOptions<TContext> options,
    ICurrentUserService currentUser)
    : DbContext(options), IUnitOfWork
    where TContext : DbContext
{
    // ── IUnitOfWork ──────────────────────────────────────────────────────────

    /// <summary>변경 사항을 DB에 저장. 감사 컬럼(CreatedAt/UpdatedAt) 자동 설정.</summary>
    public new async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        ApplyAuditColumns();
        return await base.SaveChangesAsync(ct);
    }

    /// <summary>지정된 작업을 DB 트랜잭션으로 래핑. 예외 발생 시 자동 롤백.</summary>
    public async Task ExecuteInTransactionAsync(Func<Task> action, CancellationToken ct = default)
    {
        await using var transaction = await Database.BeginTransactionAsync(ct);
        try
        {
            await action();
            await transaction.CommitAsync(ct);
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }

    // ── EF Core 모델 설정 ────────────────────────────────────────────────────

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        ApplyGlobalFilters(modelBuilder);
    }

    // ── 글로벌 쿼리 필터 ─────────────────────────────────────────────────────

    /// <summary>
    /// 런타임에 평가되는 프로퍼티.
    /// Expression.Constant(this)로 캡처되어 매 쿼리마다 현재 TenantId를 반환.
    /// </summary>
    private Guid CurrentTenantId => currentUser.TenantId;

    private void ApplyGlobalFilters(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var clrType = entityType.ClrType;
            var isTenantScoped = typeof(ITenantScoped).IsAssignableFrom(clrType);
            var isSoftDeletable = typeof(ISoftDeletable).IsAssignableFrom(clrType);

            if (!isTenantScoped && !isSoftDeletable) continue;

            var parameter = Expression.Parameter(clrType, "e");
            Expression? combined = null;

            if (isTenantScoped)
                combined = BuildTenantFilter(parameter);

            if (isSoftDeletable)
            {
                var notDeleted = BuildSoftDeleteFilter(parameter);
                combined = combined is null ? notDeleted : Expression.AndAlso(combined, notDeleted);
            }

            if (combined is not null)
                modelBuilder.Entity(clrType).HasQueryFilter(Expression.Lambda(combined, parameter));
        }
    }

    private Expression BuildTenantFilter(ParameterExpression parameter)
    {
        var property = Expression.Property(parameter, nameof(ITenantScoped.TenantId));
        // Expression.Constant(this)로 DbContext 인스턴스 캡처 → 쿼리 시 CurrentTenantId 평가
        var tenantValue = Expression.Property(
            Expression.Constant(this),
            typeof(BaseDbContext<TContext>).GetProperty(
                nameof(CurrentTenantId),
                BindingFlags.NonPublic | BindingFlags.Instance)!);
        return Expression.Equal(property, tenantValue);
    }

    private static Expression BuildSoftDeleteFilter(ParameterExpression parameter)
    {
        var property = Expression.Property(parameter, nameof(ISoftDeletable.IsDeleted));
        return Expression.Equal(property, Expression.Constant(false));
    }

    // ── 감사 컬럼 자동 설정 ──────────────────────────────────────────────────

    private void ApplyAuditColumns()
    {
        var now = DateTimeOffset.UtcNow;
        var userId = currentUser.IsAuthenticated ? currentUser.Id : (Guid?)null;

        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.SetCreatedAudit(userId, now);
                    break;
                case EntityState.Modified:
                    entry.Entity.SetUpdatedAudit(userId, now);
                    break;
            }
        }
    }
}
