export const TradeIntent = { Buy: "buy", Sell: "sell", Wait: "wait" } as const;
export type TradeIntent = typeof TradeIntent[keyof typeof TradeIntent];

export const OrderSide = { Buy: "Buy", Sell: "Sell" } as const;
export type OrderSide = typeof OrderSide[keyof typeof OrderSide];

export const OrderType = { Limit: "Limit", Market: "Market" } as const;
export type OrderType = typeof OrderType[keyof typeof OrderType];
