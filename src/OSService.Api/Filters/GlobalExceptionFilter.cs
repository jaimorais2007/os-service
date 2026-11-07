using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace OSService.Api.Filters;

public class GlobalExceptionFilter(ILogger<GlobalExceptionFilter> logger) : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        var activity = Activity.Current;
        if (activity != null)
        {
            activity.SetStatus(ActivityStatusCode.Error, context.Exception.Message);
            activity.AddException(context.Exception);
        }

        var httpMethod = context.HttpContext.Request.Method;
        var controller = context.HttpContext.Request.RouteValues["controller"]?.ToString() ?? string.Empty;
        var path = context.HttpContext.Request.Path.Value ?? string.Empty;

        logger.LogError(
            context.Exception,
            "Erro ao processar requisição no endpoint. Controller: {Controller}, Método: {HttpMethod}, Rota: {Path}, Mensagem: {ErrorMessage}",
            controller,
            httpMethod,
            path,
            context.Exception.Message
        );

        context.Result = new ObjectResult(new { message = "Ocorreu um erro interno inesperado." })
        {
            StatusCode = StatusCodes.Status500InternalServerError
        };
        context.ExceptionHandled = true;
    }
}
