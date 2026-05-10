using FinLearnApp.Domain.Entities;
using FinLearnApp.Domain.ValueObjects;

namespace FinLearnApp.Application.Actions;

/// <summary>
/// アクション実行に必要なデータアクセスを抽象化するインターフェース。
/// Application 層のハンドラーはこのインターフェースのみに依存し、
/// 具体的な保存方法（InMemoryStore 等）を知らない。
/// </summary>
public interface IActionExecutionStore
{
    Portfolio? FindPortfolioByInvestor(InvestorId investorId);
    Ticker? FindTicker(TickerId tickerId);
    int GetCurrentTurn(InvestorId investorId);
    int AdvanceTurn(InvestorId investorId);
    OrderMatchResult ExecuteBuyNow(TickerId tickerId, int quantity, Money availableCash);
    OrderMatchResult ExecuteSellNow(TickerId tickerId, int quantity);
    OrderMatchResult ExecuteBuyLimit(TickerId tickerId, int quantity, Money limitPrice, Money availableCash);
    OrderMatchResult ExecuteSellLimit(TickerId tickerId, int quantity, Money limitPrice);
}
