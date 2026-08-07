export interface Project {
  id: string;
  name: string;
  description?: string;
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
