import { Injectable, inject } from '@angular/core';
import { forkJoin, Observable } from 'rxjs';
import { map } from 'rxjs/operators';
import { ProjectService } from './project.service';
import { UserService } from './user.service';
import { Project } from '@core/models/project';
import { AdminUser } from '@core/models/admin-user';

export interface DashboardStats {
  activeProjects: number;
  deletedProjects: number;
  activeUsers: number;
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
      map(({ allProjects, deletedProjects, allUsers, deletedUsers }) => {
        const activeProjects = allProjects.filter(p => !p.isDeleted);
        const activeUsers = allUsers.filter(u => !u.isDeleted);

        return {
          activeProjects: activeProjects.length,
          deletedProjects: deletedProjects.length,
          activeUsers: activeUsers.length,
          deletedUsers: deletedUsers.length,
          recentProjects: activeProjects.slice(0, 5),
          recentUsers: activeUsers.slice(0, 5),
        };
      })
    );
  }
}