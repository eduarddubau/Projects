import { RenderMode, ServerRoute } from '@angular/ssr';

// The server can't read the JWT (it's in localStorage), so routes that show
// user-specific data are client-rendered to avoid serving an empty shell. The
// public landing page (the '**' fallback) is server-rendered; the wildcards
// (projects/**, admin/**) cover nested pages.
export const serverRoutes: ServerRoute[] = [
  {
    path: 'login',
    renderMode: RenderMode.Client
  },
  {
    path: 'register',
    renderMode: RenderMode.Client
  },
  {
    path: 'projects',
    renderMode: RenderMode.Client
  },
  {
    path: 'projects/**',
    renderMode: RenderMode.Client
  },
  {
    path: 'profile',
    renderMode: RenderMode.Client
  },
  {
    path: 'admin',
    renderMode: RenderMode.Client
  },
  {
    path: 'admin/**',
    renderMode: RenderMode.Client
  },
  {
    path: '**',
    renderMode: RenderMode.Server
  }
];