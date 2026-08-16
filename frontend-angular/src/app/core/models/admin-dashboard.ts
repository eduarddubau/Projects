import { AdminUser } from './admin-user';

// Projects appear as counts only — an aggregate carries no workspace's content.
export interface AdminDashboard {
  activeProjectCount: number;
  deletedProjectCount: number;
  activeUserCount: number;
  deletedUserCount: number;
  recentUsers: AdminUser[];
}
