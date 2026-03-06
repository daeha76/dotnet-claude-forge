using Daeha.CleanArch.Domain.Common;
using FluentAssertions;

namespace Daeha.CleanArch.Tests.Domain;

[Trait("Category", "Unit")]
public class BaseEntityTests
{
    private sealed class TestEntity : BaseEntity { }

    [Fact]
    public void NewEntity_ShouldHaveNonEmptyId()
    {
        var entity = new TestEntity();
        entity.Id.Should().NotBeEmpty();
    }

    [Fact]
    public void TwoNewEntities_ShouldHaveDifferentIds()
    {
        var e1 = new TestEntity();
        var e2 = new TestEntity();
        e1.Id.Should().NotBe(e2.Id);
    }

    [Fact]
    public void SetCreatedAudit_ShouldSetAllAuditFields()
    {
        var entity = new TestEntity();
        var userId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        entity.SetCreatedAudit(userId, now);

        entity.CreatedAt.Should().Be(now);
        entity.UpdatedAt.Should().Be(now);
        entity.CreatedBy.Should().Be(userId);
        entity.UpdatedBy.Should().Be(userId);
    }

    [Fact]
    public void SetUpdatedAudit_ShouldOnlyUpdateUpdatedFields()
    {
        var entity = new TestEntity();
        var createdBy = Guid.NewGuid();
        var createdAt = DateTimeOffset.UtcNow.AddDays(-1);
        entity.SetCreatedAudit(createdBy, createdAt);

        var updatedBy = Guid.NewGuid();
        var updatedAt = DateTimeOffset.UtcNow;
        entity.SetUpdatedAudit(updatedBy, updatedAt);

        entity.CreatedAt.Should().Be(createdAt);
        entity.CreatedBy.Should().Be(createdBy);
        entity.UpdatedAt.Should().Be(updatedAt);
        entity.UpdatedBy.Should().Be(updatedBy);
    }

    [Fact]
    public void SetCreatedAudit_WithNullUserId_ShouldSetNullAuditFields()
    {
        var entity = new TestEntity();
        entity.SetCreatedAudit(null, DateTimeOffset.UtcNow);

        entity.CreatedBy.Should().BeNull();
        entity.UpdatedBy.Should().BeNull();
    }
}
