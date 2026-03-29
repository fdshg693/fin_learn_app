import { describe, test, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import { PlayerPanel } from "./PlayerPanel";

describe("PlayerPanel", () => {
  test("現金・総資産・損益を表示する", () => {
    render(<PlayerPanel cash={100000} totalAssets={120000} profitLoss={20000} />);

    expect(screen.getByText("¥100,000")).toBeInTheDocument();
    expect(screen.getByText("¥120,000")).toBeInTheDocument();
    expect(screen.getByText("+¥20,000")).toBeInTheDocument();
  });

  test("損失時はマイナス表示", () => {
    render(<PlayerPanel cash={80000} totalAssets={80000} profitLoss={-20000} />);

    expect(screen.getByText("¥-20,000")).toBeInTheDocument();
  });
});
