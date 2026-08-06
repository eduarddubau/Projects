import { WorkspaceMember, WorkspaceRole } from './workspace';

export interface Invitation {
  id: string;
  workspaceId: string;
  email: string;
  role: WorkspaceRole;
  createdAt: string;
  expiresAt: string;
  invitedByDisplayName: string;
}

export type InviteResult =
  | { outcome: 'Joined'; token: null; member: WorkspaceMember }
  | { outcome: 'Invited'; token: string; member: null };
