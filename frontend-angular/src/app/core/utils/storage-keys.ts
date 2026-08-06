/**
 * Every localStorage key the app owns. localStorage is scoped to the origin, not to
 * the app, so anything else ever served on this port shares the bucket — the prefix
 * is what keeps them apart.
 *
 * Keep in sync with the pre-paint script in index.html, which reads THEME and PALETTE
 * as literals because it runs before any module is loaded.
 */
const PREFIX = 'pj-';

export const StorageKeys = {
  THEME: `${PREFIX}theme`,
  PALETTE: `${PREFIX}palette`,
  AUTH_TOKEN: `${PREFIX}authToken`,
  REFRESH_TOKEN: `${PREFIX}refreshToken`,
  CURRENT_WORKSPACE_ID: `${PREFIX}currentWorkspaceId`,
} as const;
