export interface MacroEventListItem {
  id: string;
  title: string;
  country: string;
  currency: string;
  category: string;
  scheduledAtUtc: number;
  importance: number;
  status: number;
  forecast?: string | null;
  previous?: string | null;
  actual?: string | null;
  blockStartUtc: number;
  blockEndUtc: number;
  isBlockingNow: boolean;
}

export interface MacroSyncResult {
  fetched: number;
  inserted: number;
  updated: number;
}

export const IMPORTANCE_LABELS: Record<number, string> = {
  0: "Unknown",
  1: "Low",
  2: "Medium",
  3: "High",
  4: "Critical"
};

export const STATUS_LABELS: Record<number, string> = {
  0: "Scheduled",
  1: "Live",
  2: "Released",
  3: "Revised",
  4: "Cancelled"
};
