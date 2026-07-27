export interface Profile {
  id: string;
  email: string;
  firstName: string;
  lastName: string;
  nickname: string | null;
  createdAt: string;
  updatedAt?: string;
}
