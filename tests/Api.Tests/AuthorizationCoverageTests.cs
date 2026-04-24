using System.Reflection;
using Api.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Tests;

public class AuthorizationCoverageTests
{
    [Fact]
    public void AllNonAuthControllers_MustHaveAuthorizeAttribute()
    {
        var violators = typeof(AuthController).Assembly
            .GetTypes()
            .Where(t => typeof(ControllerBase).IsAssignableFrom(t) && !t.IsAbstract)
            .Where(t => t != typeof(AuthController))
            .Where(t => !t.IsDefined(typeof(AuthorizeAttribute), inherit: true))
            .Select(t => t.Name)
            .ToList();

        Assert.True(violators.Count == 0,
            $"Controllers missing [Authorize]: {string.Join(", ", violators)}");
    }
}
