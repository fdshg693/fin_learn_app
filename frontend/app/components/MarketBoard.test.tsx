import { describe, test, expect, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MarketBoard } from "./MarketBoard";

const instruments = [
  { id: 1, price: 1500 },
  { id: 2, price: 3000 },
];

describe("MarketBoard", () => {
  test("銘柄一覧を表示する", () => {
    render(<MarketBoard instruments={instruments} selectedId={null} onSelect={() => {}} />);

    expect(screen.getByText("1")).toBeInTheDocument();
    expect(screen.getByText("¥1,500")).toBeInTheDocument();
    expect(screen.getByText("2")).toBeInTheDocument();
    expect(screen.getByText("¥3,000")).toBeInTheDocument();
  });

  test("行クリックでonSelectが呼ばれる", async () => {
    const user = userEvent.setup();
    const onSelect = vi.fn();
    render(<MarketBoard instruments={instruments} selectedId={null} onSelect={onSelect} />);

    await user.click(screen.getByText("¥1,500"));
    expect(onSelect).toHaveBeenCalledWith(1);
  });
});
