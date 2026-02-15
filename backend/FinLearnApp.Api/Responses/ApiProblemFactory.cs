using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace FinLearnApp.Api.Responses;

public static class ApiProblemFactory
{
    public static ObjectResult BadRequest(ControllerBase controller, string detail, string code)
    {
        var problem = CreateProblemDetails(
            status: StatusCodes.Status400BadRequest,
            title: "Bad Request",
            detail: detail,
            code: code);

        return controller.BadRequest(problem);
    }

    public static ObjectResult NotFound(ControllerBase controller, string detail, string code)
    {
        var problem = CreateProblemDetails(
            status: StatusCodes.Status404NotFound,
            title: "Not Found",
            detail: detail,
            code: code);

        return controller.NotFound(problem);
    }

    private static ProblemDetails CreateProblemDetails(int status, string title, string detail, string code)
    {
        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = detail,
        };

        problem.Extensions["code"] = code;

        return problem;
    }
}
