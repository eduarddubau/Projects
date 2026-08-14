import { Injectable, inject, signal, computed, PLATFORM_ID } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { Observable, of, tap, finalize, shareReplay } from 'rxjs';
import { WorkspaceService } from '@core/services/workspace.service';
import { Workspace, WorkspaceRole } from '@core/models/workspace';
import { StorageKeys } from '@core/utils/storage-keys';

const STORAGE_KEY = StorageKeys.CURRENT_WORKSPACE_ID;

@Injectable({ providedIn: 'root' })
export class WorkspaceContextService {
  private api = inject(WorkspaceService);
  private platformId = inject(PLATFORM_ID);

  private _workspaces = signal<Workspace[]>([]);
  private _currentWorkspaceId = signal<string | null>(null);
  private _loaded = signal(false);

  readonly workspaces = this._workspaces.asReadonly();
  readonly currentWorkspaceId = this._currentWorkspaceId.asReadonly();

  readonly personalWorkspace = computed(() => this._workspaces().find((w) => w.isPersonal) ?? null);

  readonly currentWorkspace = computed(() => {
    const id = this._currentWorkspaceId();
    return this._workspaces().find((w) => w.id === id) ?? null;
  });

  readonly myRole = computed<WorkspaceRole | null>(() => this.currentWorkspace()?.myRole ?? null);

  readonly isOwner = computed(() => this.myRole() === 'Owner');

  readonly canManageCurrent = computed(() => {
    const w = this.currentWorkspace();
    return !!w && w.myRole === 'Owner' && !w.isPersonal;
  });

  private load$: Observable<Workspace[]> | null = null;

  ensureLoaded(): Observable<Workspace[]> {
    if (this._loaded()) return of(this._workspaces());
    if (this.load$) return this.load$;

    this.load$ = this.api.getMyWorkspaces().pipe(
      tap((list) => {
        this._workspaces.set(list);
        this._loaded.set(true);
      }),
      finalize(() => (this.load$ = null)),
      shareReplay(1),
    );

    return this.load$;
  }

  refresh(): Observable<Workspace[]> {
    this._loaded.set(false);
    this.load$ = null;
    return this.ensureLoaded();
  }

  /** Best current workspace given the URL. Returns null only when there are none. */
  resolve(urlId: string | null): string | null {
    const list = this._workspaces();

    if (urlId) return list.some((w) => w.id === urlId) ? urlId : this.fallbackId();
    const stored = this.readStoredId();
    if (stored && list.some((w) => w.id === stored)) return stored;
    return this.fallbackId();
  }

  private fallbackId(): string | null {
    return this.personalWorkspace()?.id ?? this._workspaces()[0]?.id ?? null;
  }

  setCurrent(id: string): void {
    this._currentWorkspaceId.set(id);
    if (isPlatformBrowser(this.platformId)) localStorage.setItem(STORAGE_KEY, id);
  }

  private readStoredId(): string | null {
    return isPlatformBrowser(this.platformId) ? localStorage.getItem(STORAGE_KEY) : null;
  }

  private clearCurrent(): void {
    this._currentWorkspaceId.set(null);
    if (isPlatformBrowser(this.platformId)) localStorage.removeItem(STORAGE_KEY);
  }

  clear(): void {
    this._workspaces.set([]);
    this._loaded.set(false);
    this.load$ = null;
    this.clearCurrent();
  }

  upsert(workspace: Workspace): void {
    this._workspaces.update((list) => {
      const i = list.findIndex((w) => w.id === workspace.id);
      return i === -1 ? [...list, workspace] : list.with(i, workspace);
    });
  }

  remove(id: string): void {
    this._workspaces.update((list) => list.filter((w) => w.id !== id));

    if (this._currentWorkspaceId() !== id) return;

    // Restoring the invariant is the store's job — "currentWorkspaceId names a
    // workspace still in the list, or is null". Computed after the update above,
    // or the fallback picks the workspace we just removed. Navigating is the
    // caller's job; the store must not inject Router.
    const next = this.fallbackId();
    if (next) this.setCurrent(next);
    else this.clearCurrent();
  }
}
