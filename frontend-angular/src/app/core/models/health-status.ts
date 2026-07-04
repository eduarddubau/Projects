export interface HealthStatus {
  state: 'online' | 'offline';
  /** Translation key for the offline reason; the header translates it live. */
  errorKey?: string;
  errorParams?: Record<string, unknown>;
}
