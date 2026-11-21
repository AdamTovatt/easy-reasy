using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EasyReasy.Auth.Tests
{
    /// <summary>
    /// A controller of <see cref="HostedOAuthServer"/> opened to connections as a whole, with one action that has no
    /// authorization of its own and one that requires the admin role, so the class's and the action's authorization
    /// combine.
    /// </summary>
    [AllowOAuthClients]
    [Route(BasePath)]
    public sealed class OpenedConnectionsController : ControllerBase
    {
        /// <summary>
        /// The controller's route.
        /// </summary>
        public const string BasePath = "/api/opened-controller";

        /// <summary>
        /// The action with no authorization of its own.
        /// </summary>
        public const string PlainPath = BasePath + "/plain";

        /// <summary>
        /// The action requiring the admin role.
        /// </summary>
        public const string AdminPath = BasePath + "/admin";

        /// <summary>
        /// Answers the caller's subject.
        /// </summary>
        /// <returns>200 with the subject.</returns>
        [HttpGet("plain")]
        public IActionResult Plain()
        {
            return Ok(new { sub = HttpContext.GetUserId() });
        }

        /// <summary>
        /// Answers the caller's subject, for an admin.
        /// </summary>
        /// <returns>200 with the subject.</returns>
        [HttpGet("admin")]
        [Authorize(Roles = "admin")]
        public IActionResult Admin()
        {
            return Ok(new { sub = HttpContext.GetUserId() });
        }
    }
}
