using System.Threading.Tasks;
using Cloudstarter.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Cloudstarter.Controllers
{
    public class ZeebeController : Controller
    {
        private readonly ILogger<ZeebeController> _logger;
        private readonly IZeebeService _zeebeService;

        public ZeebeController(ILogger<ZeebeController> logger, IZeebeService zeebeService)
        {
            _logger = logger;
            _zeebeService = zeebeService;
        }

        [Route("/status")]
        [HttpGet]
        public async Task<IActionResult> Get()
        {
            var result = (await _zeebeService.Status()).ToString();
            return Ok(result);
        }

        [Route("/deploy")]
        [HttpGet]
        public async Task<IActionResult> DeployWorkflow()
        {
            var response = await _zeebeService.Deploy("test-throw-exception.bpmn");
            return Ok(response);
        }

        [Route("/start-workers")]
        [HttpGet]
        public IActionResult StartWorkflow()
        {
            _zeebeService.StartWorkers();
            return Ok("done");
        }

        [Route("/create-instance")]
        [HttpGet]
        public async Task<IActionResult> StartWorkflowInstance()
        {
            var instance = await _zeebeService.CreateWorkflowInstance("Process_0l9rt3b");
            return Ok(instance);
        }
    }
}