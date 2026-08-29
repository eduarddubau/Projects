using Backend.Config;
using Backend.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Controllers;

// Anonymous: the retention policy is not a secret, and the signed-out landing page
// promises recovery too. Takes TrashWindow rather than a service — there is no work
// here beyond projecting it.
[AllowAnonymous]
[ApiController]
[Route("api/config")]
public class ConfigController : ControllerBase
{
    private readonly TrashWindow _trashWindow;

    public ConfigController(TrashWindow trashWindow)
    {
        _trashWindow = trashWindow;
    }

    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<ClientConfigDto> GetConfig() =>
        Ok(new ClientConfigDto { TrashWindowDays = _trashWindow.Days });
}
