export interface HealthResponse {
  status: "connected" | "disconnected";
  walletAddress: string;
  network: string;
  timestamp: string;
  error: string | null;
}
