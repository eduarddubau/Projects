import { Project } from './project';
import { AdminUser } from './admin-user';

export interface AdminDashboard {
  activeProjectCount: number;
  deletedProjectCount: number;
  activeUserCount: number;
  deletedUserCount: number;
  recentProjects: Project[];
  recentUsers: AdminUser[];
}
