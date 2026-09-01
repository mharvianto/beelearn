using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;

namespace BeeLearn.Controllers;

[ApiController]
public abstract class ApiControllerBase : ControllerBase
{
    protected int UserId =>
        int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? throw new InvalidOperationException("no user id claim"));

    protected string CurrentRole => User.FindFirstValue(ClaimTypes.Role) ?? "Student";
}
