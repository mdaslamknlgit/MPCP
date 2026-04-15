using Microsoft.AspNetCore.Authentication;
using System.Security.Claims;

namespace MPC.PlanSched.UI.Services
{
    public class DebugClaimsTransformation : IClaimsTransformation
    {
        private readonly IWebHostEnvironment _env;
        private readonly IConfiguration _configuration;

        public DebugClaimsTransformation(IWebHostEnvironment env, IConfiguration configuration)
        {
            _env = env;
            _configuration = configuration;
        }

        public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
        {
            if (!_env.IsDevelopment())
                return Task.FromResult(principal);

            var identity = principal.Identity as ClaimsIdentity ?? throw new Exception("ClaimsIdentity was null here?");
            var debugRoles = _configuration.GetSection("RoleManagement:UserDebugRoles").Get<List<string>>() ?? [];

            if (debugRoles.Any())
            {
                var existingClaims = identity.Claims.Where(x => x.Type == ClaimTypes.Role || x.Type == "roles").ToList();
                existingClaims.ForEach(identity.RemoveClaim);
                debugRoles.ForEach(role => identity.AddClaim(new(ClaimTypes.Role, role)));
            }

            return Task.FromResult(principal);
        }
    }
}
