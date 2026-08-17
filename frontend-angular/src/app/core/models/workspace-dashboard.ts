export interface WorkspaceDashboard {
  /** Unfinished tasks across every project in the workspace. */
  openTaskCount: number;
  /** Of those, the ones assigned to the signed-in user. */
  myOpenTaskCount: number;
}
