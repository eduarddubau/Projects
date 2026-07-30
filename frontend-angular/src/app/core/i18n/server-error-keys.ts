import { HttpErrorResponse } from '@angular/common/http';

/**
 * Maps the backend's BusinessRuleCodes to transloco keys. The server sends an English
 * message alongside the code; we ignore it and translate, mirroring how the register
 * form maps Identity error codes.
 */
const SERVER_ERROR_KEYS: Record<string, string> = {
  DuplicateProjectName: 'projects.serverErrors.duplicateName',
  DuplicateEmail: 'admin.users.serverErrors.duplicateEmail',

  PersonalWorkspaceNotDeletable: 'workspaces.serverErrors.personalNotDeletable',
  PersonalWorkspaceNoMembers: 'workspaces.serverErrors.personalNoMembers',
  PersonalWorkspaceNotLeavable: 'workspaces.serverErrors.personalNotLeavable',
  AlreadyWorkspaceMember: 'workspaces.serverErrors.alreadyMember',
  WorkspaceMustHaveOwner: 'workspaces.serverErrors.mustHaveOwner',
};

/**
 * Returns the transloco key for a failed request, or `fallbackKey` when the server sent
 * no code or one this client doesn't know — a newer backend must never render a blank
 * snackbar on an older client.
 */
export function serverErrorKey(err: unknown, fallbackKey: string): string {
  const code = err instanceof HttpErrorResponse ? err.error?.code : undefined;
  return (typeof code === 'string' && SERVER_ERROR_KEYS[code]) || fallbackKey;
}
