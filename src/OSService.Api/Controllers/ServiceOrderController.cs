
using Microsoft.AspNetCore.Mvc;
using OSService.Application.Commands;
using OSService.Application.Services;
using OSService.Domain.Exceptions;

namespace OSService.Api.Controllers
{
    [ApiController]
    [Route("api/ServiceOrders")]
    public class ServiceOrderController : ControllerBase
    {
        private readonly ServiceOrderService _serviceOrderService;

        public ServiceOrderController(
            ServiceOrderService serviceOrderService)
        {
            _serviceOrderService = serviceOrderService;
        }

        [HttpPost]
        [ProducesResponseType(
            typeof(ServiceOrderGetResponse),
            StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Create(
            [FromBody] ServiceOrderRequest request)
        {
            try
            {
                var result = await _serviceOrderService.CreateAsync(request);

                return CreatedAtAction(
                    nameof(GetById),
                    new { id = result.Id },
                    result);
            }
            catch (DomainException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpGet("{id:guid}")]
        [ProducesResponseType(
            typeof(ServiceOrderGetResponse),
            StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetById(Guid id)
        {
            var result = await _serviceOrderService.GetByIdAsync(id);

            if (result == null)
            {
                return NotFound(new
                {
                    message = "Service order not found."
                });
            }

            return Ok(result);
        }

        [HttpGet]
        [ProducesResponseType(
            typeof(List<ServiceOrderGetResponse>),
            StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAll()
        {
            var result = await _serviceOrderService.GetAllAsync();

            return Ok(result);
        }

        #region Service Order Status Changes

        [HttpPost("{id:guid}/start-diagnostics")]
        public async Task<IActionResult> StartDiagnostics(Guid id)
        {
            return await ExecuteStatusChangeAsync(
                () => _serviceOrderService.StartDiagnosticsAsync(id));
        }

        [HttpPost("{id:guid}/finish-analysis")]
        public async Task<IActionResult> FinishAnalysis(
            Guid id,
            [FromBody] ServiceOrderFinishAnalysisRequest request)
        {
            return await ExecuteStatusChangeAsync(
                () => _serviceOrderService.FinishAnalysisAsync(id, request));
        }

        [HttpPost("{id:guid}/approve")]
        public async Task<IActionResult> Approve(Guid id)
        {
            return await ExecuteStatusChangeAsync(
                () => _serviceOrderService.ApproveAsync(id));
        }

        [HttpPost("{id:guid}/reject")]
        public async Task<IActionResult> Reject(Guid id)
        {
            return await ExecuteStatusChangeAsync(
                () => _serviceOrderService.RejectAsync(id));
        }

        [HttpPost("{id:guid}/deliver")]
        public async Task<IActionResult> Deliver(Guid id)
        {
            return await ExecuteStatusChangeAsync(
                () => _serviceOrderService.DeliverAsync(id));
        }

        private async Task<IActionResult> ExecuteStatusChangeAsync(
            Func<Task<ServiceOrderGetResponse>> operation)
        {
            try
            {
                var result = await operation();

                return Ok(result);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (DomainException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        #endregion
    }
}
