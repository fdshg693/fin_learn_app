import { describe, test, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import { WarningMessage } from "./WarningMessage";

describe("WarningMessage", () => {
  test("warningがnullの場合は何も表示しない", () => {
    const { container } = render(<WarningMessage warning={null} />);
    expect(container.innerHTML).toBe("");
  });

  test("警告メッセージを表示する", () => {
    render(<WarningMessage warning="残高が不足しています" />);
    expect(screen.getByText("残高が不足しています")).toBeInTheDocument();
  });
});
