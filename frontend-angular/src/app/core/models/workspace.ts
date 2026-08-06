export type WorkspaceRole = 'Owner' | 'Member';

export interface Workspace {
  id: string;
  name: string;
  description?: string;
  isPersonal: boolean;
  myRole: WorkspaceRole;
  memberCount: number;
  projectCount: number;
  createdBy?: string;
  updatedBy?: string;
  createdByDisplayName?: string;
  updatedByDisplayName?: string;
  createdAt: string;
  updatedAt?: string;
  isDeleted: boolean;
  deletedAt?: string;
}

export interface WorkspaceMember {
  workspaceId: string;
  userId: string;
  userDisplayName: string;
  role: WorkspaceRole;
  joinedAt: string;
}
