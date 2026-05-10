using FinLearnApp.Domain.Entities;

namespace FinLearnApp.Application.Actions;

public enum ActionExecutionStatus
{
    Ok,
    BadRequest,
    NotFound,
    Conflict,
}

/// <summary>
/// アクション実行の結果を表すアプリケーション層の戻り値。
/// </summary>
public sealed class ActionExecutionResult
{
    public ActionExecutionStatus Status { get; }
    public bool Success { get; }
    public string? Message { get; }
    public Portfolio? Portfolio { get; }
    public int? CurrentTurn { get; }

    private ActionExecutionResult(
        ActionExecutionStatus status,
        bool success,
        string? message,
        Portfolio? portfolio,
        int? currentTurn)
    {
        Status = status;
        Success = success;
        Message = message;
        Portfolio = portfolio;
        CurrentTurn = currentTurn;
    }

    public static ActionExecutionResult Ok(bool success, string message, Portfolio portfolio, int currentTurn)
    {
        return new ActionExecutionResult(ActionExecutionStatus.Ok, success, message, portfolio, currentTurn);
    }

    public static ActionExecutionResult BadRequest(string message)
    {
        return new ActionExecutionResult(ActionExecutionStatus.BadRequest, false, message, null, null);
    }

    public static ActionExecutionResult NotFound()
    {
        return new ActionExecutionResult(ActionExecutionStatus.NotFound, false, null, null, null);
    }

    public static ActionExecutionResult Conflict(string message, int currentTurn)
    {
        return new ActionExecutionResult(ActionExecutionStatus.Conflict, false, message, null, currentTurn);
    }
}
