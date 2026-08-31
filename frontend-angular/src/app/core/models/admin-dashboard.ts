import { AdminUser } from './admin-user';

/**
 * The platform admin's at-a-glance view.
 *
 * Projects, workspaces and tasks appear as counts only — an aggregate carries no
 * workspace's content. Accounts are the admin's own domain, so they arrive as rows.
 */
export interface AdminDashboard {
  // What the instance holds.
  activeUserCount: number;
  /** Shared only — every account holds an undeletable personal one. */
  sharedWorkspaceCount: number;
  activeProjectCount: number;
  taskCount: number;

  // What is waiting on a decision.
  purgeableProjectCount: number;
  deletedUserCount: number;
  lockedOutUserCount: number;

  // Context.
  deletedProjectCount: number;
  deletedWorkspaceCount: number;
  newUserCount: number;
  /** How far back "new" reaches; the server's number, never assumed here. */
  newUserWindowDays: number;
  environment: string;

  recentUsers: AdminUser[];
}
