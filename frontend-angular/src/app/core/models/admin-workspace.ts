/**
 * A workspace as the admin area sees it. Deliberately without `myRole`: an
 * administrator belongs to no workspace, so the API does not send one.
 */
export interface AdminWorkspace {
  id: string;
  name: string;
  description?: string;
  isPersonal: boolean;
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
