import { memo, useState, useCallback } from "react";
import { Form, useNavigation } from "react-router";
import type { InstrumentDto } from "~/types/game";

type Props = {
  instruments: InstrumentDto[];
  selectedInstrumentId: number | null;
  onInstrumentChange: (id: number) => void;
};

export const TradeForm = memo(function TradeForm({ instruments, selectedInstrumentId, onInstrumentChange }: Props) {
  const [quantity, setQuantity] = useState(1);
  const [price, setPrice] = useState("");
  const navigation = useNavigation();
  const isSubmitting = navigation.state === "submitting";

  const instrumentId = selectedInstrumentId ?? instruments[0]?.id ?? 0;

  const handleInstrumentChange = useCallback(
    (e: React.ChangeEvent<HTMLSelectElement>) => onInstrumentChange(Number(e.target.value)),
    [onInstrumentChange],
  );
  const handleQuantityChange = useCallback(
    (e: React.ChangeEvent<HTMLInputElement>) => setQuantity(Number(e.target.value)),
    [],
  );
  const handlePriceChange = useCallback(
    (e: React.ChangeEvent<HTMLInputElement>) => setPrice(e.target.value),
    [],
  );

  return (
    <div className="bg-white dark:bg-gray-800 rounded-lg p-4 shadow">
      <h2 className="text-sm font-semibold text-gray-500 mb-3">注文</h2>

      <div className="space-y-3">
        <div>
          <label className="block text-xs text-gray-500 mb-1">銘柄</label>
          <select
            value={instrumentId}
            onChange={handleInstrumentChange}
            className="w-full border rounded px-3 py-2 text-sm dark:bg-gray-700 dark:border-gray-600"
          >
            {instruments.map((inst) => (
              <option key={inst.id} value={inst.id}>
                銘柄 {inst.id} (¥{inst.price.toLocaleString()})
              </option>
            ))}
          </select>
        </div>

        <div>
          <label className="block text-xs text-gray-500 mb-1">数量</label>
          <input
            type="number"
            min={1}
            value={quantity}
            onChange={handleQuantityChange}
            className="w-full border rounded px-3 py-2 text-sm dark:bg-gray-700 dark:border-gray-600"
          />
        </div>

        <div>
          <label className="block text-xs text-gray-500 mb-1">価格（空欄＝成行）</label>
          <input
            type="number"
            min={1}
            value={price}
            onChange={handlePriceChange}
            placeholder="成行"
            className="w-full border rounded px-3 py-2 text-sm dark:bg-gray-700 dark:border-gray-600"
          />
        </div>
      </div>

      <div className="flex gap-2 mt-4">
        <Form method="post" className="flex-1">
          <input type="hidden" name="intent" value="buy" />
          <input type="hidden" name="instrumentId" value={instrumentId} />
          <input type="hidden" name="quantity" value={quantity} />
          <input type="hidden" name="price" value={price} />
          <button
            type="submit"
            disabled={isSubmitting}
            className="w-full bg-red-500 hover:bg-red-600 text-white font-bold py-2 rounded disabled:opacity-50"
          >
            買う
          </button>
        </Form>

        <Form method="post" className="flex-1">
          <input type="hidden" name="intent" value="sell" />
          <input type="hidden" name="instrumentId" value={instrumentId} />
          <input type="hidden" name="quantity" value={quantity} />
          <input type="hidden" name="price" value={price} />
          <button
            type="submit"
            disabled={isSubmitting}
            className="w-full bg-blue-500 hover:bg-blue-600 text-white font-bold py-2 rounded disabled:opacity-50"
          >
            売る
          </button>
        </Form>

        <Form method="post" className="flex-1">
          <input type="hidden" name="intent" value="wait" />
          <button
            type="submit"
            disabled={isSubmitting}
            className="w-full bg-gray-400 hover:bg-gray-500 text-white font-bold py-2 rounded disabled:opacity-50"
          >
            待つ
          </button>
        </Form>
      </div>
    </div>
  );
});
