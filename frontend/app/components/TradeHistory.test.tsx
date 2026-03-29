import { describe, test, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import { TradeHistory } from "./TradeHistory";

describe("TradeHistory", () => {
  test("約定がない場合は何も表示しない", () => {
    const { container } = render(<TradeHistory trades={[]} />);
    expect(container.innerHTML).toBe("");
  });

  test("約定結果を表示する", () => {
    const trades = [
      { instrumentId: 1, side: "Buy", filledQuantity: 5, totalAmount: 7500, fee: 100 },
    ];
    render(<TradeHistory trades={trades} />);

    expect(screen.getByText("買")).toBeInTheDocument();
    expect(screen.getByText("5")).toBeInTheDocument();
    expect(screen.getByText("¥7,500")).toBeInTheDocument();
    expect(screen.getByText("¥100")).toBeInTheDocument();
  });

  test("売り注文は「売」と表示される", () => {
    const trades = [
      { instrumentId: 2, side: "Sell", filledQuantity: 3, totalAmount: 9000, fee: 50 },
    ];
    render(<TradeHistory trades={trades} />);

    expect(screen.getByText("売")).toBeInTheDocument();
  });
});
