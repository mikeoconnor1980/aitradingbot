export interface ExecutionLogEntry {
  agentId: string;
  timestampUtc: string;
  category: string;
  level: string;
  message: string;
  data: Record<string, unknown> | null;
}
