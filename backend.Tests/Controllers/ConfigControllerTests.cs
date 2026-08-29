using Backend.Config;
using Backend.Controllers;
using Backend.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Tests.Controllers;

public class ConfigControllerTests
{
    [Fact]
    public void GetConfig_PublishesTheConfiguredTrashWindow()
    {
        var controller = new ConfigController(new TrashWindow { Days = 14 });

        var result = controller.GetConfig();

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var config = Assert.IsType<ClientConfigDto>(okResult.Value);
        Assert.Equal(14, config.TrashWindowDays);
    }
}
