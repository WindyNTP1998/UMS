using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace UMS.Platform.Common.Platform.Controller;

public class PlatformController : ControllerBase
{
    private readonly IMediator _mediator;

    public PlatformController(IMediator mediator)
    {
        _mediator = mediator;
    }
}