import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { API_URL } from '@core/tokens/app.tokens';
import { Workspace, WorkspaceRole } from '@core/models/workspace';
import { Invitation, InviteResult } from '@core/models/invitation';

@Injectable({ providedIn: 'root' })
export class InvitationService {
  private http = inject(HttpClient);
  private apiUrl = inject(API_URL);

  getPending(workspaceId: string): Observable<Invitation[]> {
    return this.http.get<Invitation[]>(`${this.apiUrl}/workspaces/${workspaceId}/invitations`);
  }

  invite(workspaceId: string, email: string, role: WorkspaceRole): Observable<InviteResult> {
    return this.http.post<InviteResult>(
      `${this.apiUrl}/workspaces/${workspaceId}/invitations`,
      { email, role }
    );
  }

  revoke(workspaceId: string, invitationId: string): Observable<void> {
    return this.http.delete<void>(
      `${this.apiUrl}/workspaces/${workspaceId}/invitations/${invitationId}`
    );
  }

  accept(token: string): Observable<Workspace> {
    return this.http.post<Workspace>(`${this.apiUrl}/invitations/accept`, { token });
  }
}
