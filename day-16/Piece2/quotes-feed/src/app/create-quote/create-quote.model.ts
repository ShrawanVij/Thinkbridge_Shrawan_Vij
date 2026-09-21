export interface CreateQuoteRequest {
  author: string;
  text: string;
}

export interface CreateQuoteResult {
  id: number;
  author: string;
  text: string;
  userId: number;
  createdAt: string;
}

export interface ValidationProblemDetails {
  title: string;
  status: number;
  errors: Record<string, string[]>;
}