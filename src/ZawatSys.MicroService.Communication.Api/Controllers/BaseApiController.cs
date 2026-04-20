using Microsoft.AspNetCore.Mvc;
using ZawatSys.MicroService.Communication.Api.Extensions;
using ZawatSys.MicroLib.Shared.Contracts.Responses;

namespace ZawatSys.MicroService.Communication.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public abstract class BaseApiController : ControllerBase
{
    protected IActionResult MapToHttpResponse<T>(InternalResponse<T> response)
    {
        if (response.IsSuccess)
        {
            return Ok(response);
        }

        var statusCode = InternalResponseHttpMapper.ToStatusCode(response.MsgCode);
        return StatusCode(statusCode, response);
    }

    protected IActionResult MapToCreatedHttpResponse<T>(InternalResponse<T> response, string? location = null)
    {
        if (response.IsSuccess)
        {
            return Created(location ?? string.Empty, response);
        }

        return StatusCode(InternalResponseHttpMapper.ToStatusCode(response.MsgCode), response);
    }

    protected IActionResult MapToNoContentHttpResponse<T>(InternalResponse<T> response)
    {
        if (response.IsSuccess)
        {
            return NoContent();
        }

        return StatusCode(InternalResponseHttpMapper.ToStatusCode(response.MsgCode), response);
    }
}
