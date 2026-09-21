export class AppError extends Error {
  constructor(
    readonly friendlyMessage: string,
    readonly status: number,
    readonly fieldErrors?: Record<string, string[]>,
  ) {
    super(friendlyMessage);
    this.name = 'AppError';
  }
}