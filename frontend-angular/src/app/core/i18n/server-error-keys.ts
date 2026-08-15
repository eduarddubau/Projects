import { HttpErrorResponse } from '@angular/common/http';

const SERVER_ERROR_KEYS: Record<string, string> = {
  DuplicateProjectName: 'projects.serverErrors.duplicateName',
  DuplicateEmail: 'admin.users.serverErrors.duplicateEmail',
  ValueTooLong: 'common.errors.valueTooLong',

  PersonalWorkspaceNotDeletable: 'workspaces.serverErrors.personalNotDeletable',
  PersonalWorkspaceNotRenamable: 'workspaces.serverErrors.personalNotRenamable',
  PersonalWorkspaceNoMembers: 'workspaces.serverErrors.personalNoMembers',
  PersonalWorkspaceNotLeavable: 'workspaces.serverErrors.personalNotLeavable',
  AlreadyWorkspaceMember: 'workspaces.serverErrors.alreadyMember',
  WorkspaceHasProjects: 'workspaces.serverErrors.hasProjects',
  WorkspaceMustHaveOwner: 'workspaces.serverErrors.mustHaveOwner',
  SoleOwnerOfWorkspaces: 'admin.users.serverErrors.soleOwnerOfWorkspaces',
  EmailReclaimed: 'admin.trashUsers.serverErrors.emailReclaimed',

  PendingInvitationExists: 'invitations.serverErrors.pendingExists',
  InvitationInvalid: 'invitations.serverErrors.invalid',
  EmailBelongsToDeletedAccount: 'invitations.serverErrors.deletedAccount',
};

export function serverErrorKey(err: unknown, fallbackKey: string): string {
  const code = err instanceof HttpErrorResponse ? err.error?.code : undefined;
  return (typeof code === 'string' && SERVER_ERROR_KEYS[code]) || fallbackKey;
}

export function serverErrorParams(err: unknown): Record<string, string> | undefined {
  const params = err instanceof HttpErrorResponse ? err.error?.params : undefined;
  return params && typeof params === 'object' ? params : undefined;
}
