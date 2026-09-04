export interface UserDto {
  id: number;
  email: string;
  fio: string;
  phoneNumber?: string;
  isAdmin: boolean;
  isSender: boolean;
  isRegistrar: boolean;
}

export interface AuthResponse {
  token: string;
  user: UserDto;
}

export interface ZipDto {
  id: string;
  /** Наименование приходит из PartNumbers.Name */
  name: string;
  incomeCost: number;
  /** Цена продажи. Именно её видит покупатель — incomeCost закупочная. */
  sellCost?: number;
  partNum?: string;
  /** Марки, к которым применима деталь (через PartNumberApplicability) */
  marks: string[];
  /** Модели, к которым применима деталь (через PartNumberApplicability) */
  models: string[];
  group?: string;
  year?: number;
  incomeMotoId: string;
  countStored: number;
  photos: string[];
  /** Заметка о состоянии конкретной детали — заполняется в админке */
  comment?: string;
}

export interface PagedResult<T> {
  items: T[];
  total: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

export interface MarkDto { id: number; mark: string; }
export interface ModelDto { id: number; markId?: number; model: string; }
export interface GroupDto { id: number; groupName: string; }

export interface OrderDto {
  id: string;
  orderNumber: string;
  countOrdered: number;
  orderDateTime: string;
  zipName?: string;
  zipCost?: number;
  address: string;
  sellCost: number;
  discount?: number;
  /** Общие на всю покупку — одинаковы у всех её позиций (см. Purchase на бэкенде). */
  isPaid: boolean;
  /** Имя файла чека, если покупатель уже приложил его (см. OrdersService.uploadReceipt) — один на всю покупку. */
  receiptFileName?: string;
  /** Корзина, к которой относится эта позиция — оплата и чек привязаны к ней, не к самой позиции. */
  purchaseId: string;
  purchaseNumber: string;
}

/** Корзина целиком — то, что возвращает оформление заказа (см. OrdersService.checkout). */
export interface PurchaseDto {
  id: string;
  purchaseNumber: string;
  items: OrderDto[];
}

/** Одна позиция корзины при оформлении. */
export interface CartItemRequest {
  zipId: string;
  count: number;
  sellCost: number;
}

export interface AddressDto {
  id: number;
  address: string;
  postCode?: string;
}

// ---------- Отчёты ----------

export interface StockReportRow {
  zipId: string;
  name: string;
  partNum?: string;
  count: number;
  sellCost?: number;
  total: number;
  incomeMoto?: string;
}

export interface SalesReportRow {
  zipId: string;
  name: string;
  partNum?: string;
  incomeMoto?: string;
  sold: number;
  revenue: number;
  cost: number;
  margin: number;
}

export interface IncomeReportRow {
  createdAt: string;
  zipId: string;
  name: string;
  partNum?: string;
  qty: number;
  unitCost?: number;
  total: number;
  incomeMoto?: string;
}

export interface PriceHistoryRow {
  id: number;
  createdAt: string;
  zipId: string;
  name: string;
  partNum?: string;
  oldCost: number;
  newCost: number;
  delta: number;
  operation: string;
  user?: string;
  comment?: string;
}

// ---------- Поддержка ----------

export interface SupportMessageDto {
  id: number;
  isFromAdmin: boolean;
  authorName: string;
  text: string;
  createdAt: string;
}

export interface SupportTicketDto {
  id: string;
  orderNumber?: string;
  orderId?: string;
  subject?: string;
  createdAt: string;
  isClosed: boolean;
  messages: SupportMessageDto[];
}

export interface SupportTicketSummaryDto {
  id: string;
  orderNumber?: string;
  subject?: string;
  createdAt: string;
  lastMessageAt: string;
  lastMessagePreview: string;
  isClosed: boolean;
  userFio?: string;
  userEmail?: string;
}

export interface ZipHistoryRow {
  id: number;
  createdAt: string;
  operation: string;
  qty?: number;
  unitCost?: number;
  sellCost?: number;
  orderId?: string;
  user?: string;
  description?: string;
}
