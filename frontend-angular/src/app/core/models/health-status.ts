export interface HealthStatus {
  state: 'online' | 'offline';
  error?: string;
}