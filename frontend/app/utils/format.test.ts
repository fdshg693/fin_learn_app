import { describe, test, expect } from "vitest";
import { formatJPY } from "./format";

describe("formatJPY", () => {
  test("正の金額をJPY表記にする", () => {
    expect(formatJPY(1000)).toBe("¥1,000");
  });

  test("0を表示する", () => {
    expect(formatJPY(0)).toBe("¥0");
  });

  test("負の金額を表示する", () => {
    expect(formatJPY(-500)).toBe("¥-500");
  });

  test("大きな金額にカンマを入れる", () => {
    expect(formatJPY(1000000)).toBe("¥1,000,000");
  });
});
