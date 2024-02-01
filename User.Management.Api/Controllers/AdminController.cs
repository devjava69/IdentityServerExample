using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace User.Management.Api.Controllers
{
    [Authorize]
    [Route("api/[Controller]")]
    [ApiController]
    public class AdminController: ControllerBase
    {
        [Authorize(Roles = "HR")]
        [HttpGet]
        public IEnumerable<string> Get()
        {
            return new List<string> { "Ahmed", "Ali", "Ahsan" };
        }
    }
}
