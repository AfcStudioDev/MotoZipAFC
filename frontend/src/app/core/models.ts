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
  /** Подтверждает администратор вручную — онлайн-оплата сейчас отключена. */
  isPaid: boolean;
  /** Имя файла чека, если покупатель уже приложил его (см. OrdersService.uploadReceipt). */
  receiptFileName?: string;
}

export interface AddressDto {
  id: number;
  address: string;
  postCode?: string;
}

// ---------- Отчёты ----------

export interface SalesReportRow {
  zipId: string;
  name: string;
  partNum?: string;
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
