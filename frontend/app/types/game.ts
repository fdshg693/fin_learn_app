export type GameResponse = {
  gameId: string;
  turn: number;
  player: PlayerDto;
  instruments: InstrumentDto[];
  warning: string | null;
};

export type PlayerDto = {
  name: string;
  cash: number;
  positions: PositionDto[];
  totalAssets: number;
  profitLoss: number;
};

export type PositionDto = {
  instrumentId: number;
  quantity: number;
  currentPrice: number;
  amount: number;
};

export type InstrumentDto = {
  id: number;
  price: number;
};

export type OrderRequest = {
  instrumentId: number;
  quantity: number;
  price: number | null;
};

export type OrderBookResponse = {
  orders: OrderDto[];
};

export type OrderDto = {
  id: number;
  traderId: string;
  instrumentId: number;
  side: string;
  type: string;
  quantity: number;
  price: number | null;
  stopPrice: number | null;
  createdAtTurn: number;
};
