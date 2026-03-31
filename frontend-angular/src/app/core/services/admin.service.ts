import { Injectable, inject } from '@angular/core';
import { forkJoin, Observable } from 'rxjs';
import { map } from 'rxjs/operators';
import { ProjectService } from './project.service';
import { UserService } from './user.service';
import { Project } from '@core/models/project';
import { AdminUser } from '@core/models/admin-user';

export interface DashboardStats {
  totalProjects: number;
  deletedProjects: number;
  totalUsers: number;
  deletedUsers: number;
  recentProjects: Project[];
  recentUsers: AdminUser[];
}

@Injectable({ providedIn: 'root' })
export class AdminService {
  private projectService = inject(ProjectService);
  private userService = inject(UserService);

  getDashboardStats(): Observable<DashboardStats> {
    return forkJoin({
      allProjects: this.projectService.getAllProjects(),
      deletedProjects: this.projectService.getDeletedProjects(),
      allUsers: this.userService.getAllUsers(),
      deletedUsers: this.userService.getDeletedUsers(),
    }).pipe(
      map(({ allProjects, deletedProjects, allUsers, deletedUsers }) => ({
        totalProjects: allProjects.length,
        deletedProjects: deletedProjects.length,
        totalUsers: allUsers.length,
        deletedUsers: deletedUsers.length,
        recentProjects: allProjects.slice(0, 5),
        recentUsers: allUsers.slice(0, 5),
      }))
    );
  }
}