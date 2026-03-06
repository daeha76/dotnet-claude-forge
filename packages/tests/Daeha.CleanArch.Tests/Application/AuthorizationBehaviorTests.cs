using Daeha.CleanArch.Application.Common.Behaviors;
using Daeha.CleanArch.Application.Common.Interfaces;
using FluentAssertions;
using MediatR;
using NSubstitute;

namespace Daeha.CleanArch.Tests.Application;

[Trait("Category", "Unit")]
public class AuthorizationBehaviorTests
{
    private record UnauthorizedRequest : IRequest<string>;

    private record AdminRequest : IRequest<string>, IAuthorizedRequest
    {
        public IReadOnlyList<string> RequiredRoles => ["system_admin", "company_admin"];
    }

    [Fact]
    public async Task Handle_WithoutIAuthorizedRequest_ShouldCallNext()
    {
        var currentUser = Substitute.For<ICurrentUserService>();
        var behavior = new AuthorizationBehavior<UnauthorizedRequest, string>(currentUser);
        var nextCalled = false;

        await behavior.Handle(
            new UnauthorizedRequest(),
            (ct) => { nextCalled = true; return Task.FromResult("ok"); },
            CancellationToken.None);

        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WithMatchingRole_ShouldCallNext()
    {
        var currentUser = Substitute.For<ICurrentUserService>();
        currentUser.Role.Returns("system_admin");
        var behavior = new AuthorizationBehavior<AdminRequest, string>(currentUser);
        var nextCalled = false;

        await behavior.Handle(
            new AdminRequest(),
            (ct) => { nextCalled = true; return Task.FromResult("ok"); },
            CancellationToken.None);

        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WithNonMatchingRole_ShouldThrowUnauthorizedAccessException()
    {
        var currentUser = Substitute.For<ICurrentUserService>();
        currentUser.Role.Returns("member");
        var behavior = new AuthorizationBehavior<AdminRequest, string>(currentUser);

        var act = async () => await behavior.Handle(
            new AdminRequest(),
            (ct) => Task.FromResult("ok"),
            CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }
}
