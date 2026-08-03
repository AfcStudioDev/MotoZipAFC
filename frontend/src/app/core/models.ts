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
  name: string;
  incomeCost: number;
  partNumber?: string;
  mark?: string;
  model?: string;
  group?: string;
  year?: number;
  incomeMotoId: string;
  countStored: number;
  photos: string[];
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
  paymentStatus?: string;
  sellCost: number;
}

export interface AddressDto {
  id: number;
  address: string;
  postCode?: string;
}
