import { describe, test, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import { GameHeader } from "./GameHeader";

describe("GameHeader", () => {
  test("ターン数とプレイヤー名を表示する", () => {
    render(<GameHeader turn={3} playerName="テストプレイヤー" />);

    expect(screen.getByText("ターン 3")).toBeInTheDocument();
    expect(screen.getByText("テストプレイヤー")).toBeInTheDocument();
  });
});
