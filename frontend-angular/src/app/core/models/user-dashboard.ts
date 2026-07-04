import { Project } from './project';

export interface UserDashboard {
  activeProjectCount: number;
  deletedProjectCount: number;
  lastActivityAt?: string;
  recentProjects: Project[];
}
