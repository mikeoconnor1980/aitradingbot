export interface ErrorDto {
  errorMessage: string;
  errorCode: string | null;
  correlationId: string;
  timestamp: string;
}
