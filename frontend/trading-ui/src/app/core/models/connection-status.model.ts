export type ConnectionState = "Connected" | "Reconnecting" | "Disconnected";

export interface ConnectionStatus {
  source: string;
  status: ConnectionState;
  detail: string | null;
  retryCount: number;
}
