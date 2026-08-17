export interface Project {
  id: string;
  name: string;
  description?: string;
  workspaceId: string;
  workspaceName: string;
  createdBy?: string;
  updatedBy?: string;
  createdByDisplayName?: string;
  updatedByDisplayName?: string;
  createdAt: string;
  updatedAt?: string;
  isDeleted: boolean;
  deletedAt?: string;
  isPurgeable: boolean;
}

/** A move drops assignees the target workspace does not contain, so it reports how many. */
export interface MoveProjectResult {
  project: Project;
  unassignedTaskCount: number;
}
