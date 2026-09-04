export type UserRole = "Auditor" | "Manager" | "BranchStaff" | "Owner" | "Buyer" | "Admin";

export interface UserProfile {
  id: number;
  name: string;
  email: string;
  role: UserRole;
  establishmentId?: number;
  branchName?: string;
  pcfBalance?: number;
  dailyStartingFloat?: number;
}

export interface LoginResponse {
  token: string;
  user: UserProfile;
}

export interface AuditItemDetailDto {
  id: number;
  itemName: string;
  quantity: number;
  price: number;
  total: number;
  source: string;
  expenseSourceName?: string;
  expenseSourceId?: number;
  allocation: string;
  hasReceipt: boolean;
  status: string;
}

export interface AuditItemDto {
  id: number;
  amount: number;
  description: string;
  entryDate: string;
  submittedAt?: string;
  status: string;
  notes?: string;
  buyer: { id: number; name: string; email: string };
  establishment: { id: number; name: string };
  images: string[];
  receiptImageUrl?: string;
  details: AuditItemDetailDto[];
}

export interface AuditsByDateResponse {
  selectedDate: string;
  stats: {
    totalAmount: number;
    totalCount: number;
    totalReceiptPhotos: number;
    totalLineItems: number;
    approvedCount: number;
    pendingCount: number;
  };
  recentActiveDates: Array<{
    date: string;
    label: string;
    count: number;
    total: number;
  }>;
  audits: AuditItemDto[];
}

export interface ManagerDashboardResponse {
  currentPcf: number;
  startingFloat: number;
  cashInToday: number;
  cashOutToday: number;
  isSafeFloat: boolean;
  pendingApprovalsCount: number;
  pendingSurrendersCount: number;
  branchName: string;
}

export interface PendingDeliveryDto {
  id: number;
  auditItemId: number;
  itemName: string;
  quantity: number;
  price: number;
  total: number;
  source: string;
  date: string;
  buyer: { id: number; name: string };
  hasReceipt: boolean;
  receiptImageUrl?: string;
}
