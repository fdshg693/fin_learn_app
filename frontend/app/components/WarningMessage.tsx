import { memo } from "react";

type Props = {
  warning: string | null;
};

export const WarningMessage = memo(function WarningMessage({ warning }: Props) {
  if (!warning) return null;

  return (
    <div className="bg-yellow-50 border border-yellow-300 text-yellow-800 px-4 py-3 rounded-lg">
      {warning}
    </div>
  );
});
