using Microsoft.AspNetCore.Mvc;
using NATKScheduleAPI.Services;

namespace NATKScheduleAPI.Controllers
{
    [ApiController]
    [Route("api")]
    public class NATKScheduleController : ControllerBase
    {

        [HttpGet("groups")]
        public async Task<IEnumerable<GroupInfo>> Get()
        {
            return await new NatkParser().GetGroupsAsync();
        }

        [HttpGet("schedule/{url}")]
        public async Task<GroupSchedule?> Get(string url)
        {
            return await new NatkParser().GetGroupScheduleAsync(url);
        }
    }
}
