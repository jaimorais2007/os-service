using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging.Abstractions;
using OSService.Api.Filters;
using Xunit;

namespace OSService.Tests.Filters;

public class GlobalExceptionFilterTests
{
    private static ExceptionContext CreateContext(Exception exception, string controllerName = "ServiceOrder", string httpMethod = "GET", string path = "/api/ServiceOrders")
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = httpMethod;
        httpContext.Request.Path = path;

        var routeData = new RouteData();
        routeData.Values["controller"] = controllerName;

        var actionContext = new ActionContext(
            httpContext,
            routeData,
            new ActionDescriptor());

        return new ExceptionContext(actionContext, [])
        {
            Exception = exception
        };
    }

    [Fact]
    public void OnException_DeveRetornarInternalServerError_E_MarcarComoTratado()
    {
        using var activity = new Activity("test-operation").Start();
        var filter = new GlobalExceptionFilter(NullLogger<GlobalExceptionFilter>.Instance);
        var context = CreateContext(new InvalidOperationException("Erro inesperado no banco de dados."));

        filter.OnException(context);

        var result = Assert.IsType<ObjectResult>(context.Result);
        Assert.Equal(StatusCodes.Status500InternalServerError, result.StatusCode);
        Assert.True(context.ExceptionHandled);
    }

    [Fact]
    public void OnException_SemActivity_DeveExecutarComSucesso()
    {
        Activity.Current = null;
        var filter = new GlobalExceptionFilter(NullLogger<GlobalExceptionFilter>.Instance);
        var context = CreateContext(new Exception("Falha geral"));

        filter.OnException(context);

        var result = Assert.IsType<ObjectResult>(context.Result);
        Assert.Equal(StatusCodes.Status500InternalServerError, result.StatusCode);
        Assert.True(context.ExceptionHandled);
    }
}
