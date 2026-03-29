import { describe, test, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import { PositionList } from "./PositionList";

describe("PositionList", () => {
  test("ポジションなしのメッセージを表示", () => {
    render(<PositionList positions={[]} />);

    expect(screen.getByText("ポジションなし")).toBeInTheDocument();
  });

  test("保有ポジションを表示する", () => {
    const positions = [
      { instrumentId: 1, quantity: 10, currentPrice: 1500, amount: 15000 },
    ];
    render(<PositionList positions={positions} />);

    expect(screen.getByText("10")).toBeInTheDocument();
    expect(screen.getByText("¥1,500")).toBeInTheDocument();
    expect(screen.getByText("¥15,000")).toBeInTheDocument();
  });
});
