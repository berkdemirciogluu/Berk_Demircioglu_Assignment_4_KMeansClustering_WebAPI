using Business.Abstract;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace WebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ContainersOptimizationController : ControllerBase
    {
        IContainerService _containerService; //To avoid instance generation process everytime,injection was implemented.

        public ContainersOptimizationController(IContainerService containerService)
        {
            _containerService = containerService;
        }

        [HttpGet("GetCLusteredContainersUsingKMeans")]
        // The clustering of the containers that belong to the specified vehicle is optimized using K-Means algorithm.
        public IActionResult GetOptimizedContainers(long vehicleId, int clusterNumber)
        {
            var result = _containerService.GetOptimizedContainers(vehicleId, clusterNumber);
            if (result.Success)
                return Ok(result);
            return BadRequest(result);
        }
    }
}
