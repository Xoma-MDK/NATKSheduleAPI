using Microsoft.AspNetCore.Mvc;
using NATKScheduleAPI.Services;

namespace NATKScheduleAPI.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class NATKScheduleController : ControllerBase
    {

        [HttpGet()]
        public async Task<IEnumerable<GroupInfo>> Get()
        {
            return await new NatkParser().GetGroupsAsync();
        }

        [HttpGet("{url}")]
        public async Task<GroupSchedule?> Get(string url)
        {
            return await new NatkParser().GetGroupScheduleAsync(url);
        }
    }
}
