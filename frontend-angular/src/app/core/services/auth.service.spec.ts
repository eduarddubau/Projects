import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Router } from '@angular/router';
import { AuthService } from './auth.service';
import { API_URL } from '@core/tokens/app.tokens';
import { AuthResponse } from '@core/models/auth-response';

const apiUrl = 'http://api.test';

function authResponse(token: string, refreshToken: string): AuthResponse {
  return {
    token,
    refreshToken,
    user: { id: '1', email: 'a@b.com', firstName: 'A', lastName: 'B', isAdmin: false },
  };
}

describe('AuthService', () => {
  let service: AuthService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: API_URL, useValue: apiUrl },
        { provide: Router, useValue: { navigate: () => Promise.resolve(true) } },
      ],
    });
    service = TestBed.inject(AuthService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('stores both tokens on login', () => {
    service.login({ email: 'a@b.com', password: 'x' }).subscribe();

    const req = httpMock.expectOne(`${apiUrl}/auth/login`);
    req.flush(authResponse('access-1', 'refresh-1'));

    expect(localStorage.getItem('authToken')).toBe('access-1');
    expect(localStorage.getItem('refreshToken')).toBe('refresh-1');
  });

  it('refresh() posts the stored token and stores the rotated pair', () => {
    localStorage.setItem('refreshToken', 'refresh-old');
    let newAccess: string | undefined;

    service.refresh().subscribe(token => (newAccess = token));

    const req = httpMock.expectOne(`${apiUrl}/auth/refresh`);
    expect(req.request.body).toEqual({ refreshToken: 'refresh-old' });
    req.flush(authResponse('access-2', 'refresh-2'));

    expect(newAccess).toBe('access-2');
    expect(localStorage.getItem('authToken')).toBe('access-2');
    expect(localStorage.getItem('refreshToken')).toBe('refresh-2');
  });

  it('refresh() is single-flight: concurrent callers share one request', () => {
    localStorage.setItem('refreshToken', 'refresh-old');

    service.refresh().subscribe();
    service.refresh().subscribe();

    const req = httpMock.expectOne(`${apiUrl}/auth/refresh`); // exactly one despite two callers
    req.flush(authResponse('access-2', 'refresh-2'));
    httpMock.expectNone(`${apiUrl}/auth/refresh`);
  });

  it('refresh() errors without hitting the network when no refresh token is stored', () => {
    let errored = false;

    service.refresh().subscribe({ error: () => (errored = true) });

    expect(errored).toBe(true);
    httpMock.expectNone(`${apiUrl}/auth/refresh`);
  });

  it('logout() revokes the token server-side and clears storage', () => {
    localStorage.setItem('authToken', 'access-1');
    localStorage.setItem('refreshToken', 'refresh-1');

    service.logout();

    const req = httpMock.expectOne(`${apiUrl}/auth/logout`);
    expect(req.request.body).toEqual({ refreshToken: 'refresh-1' });
    req.flush(null);

    expect(localStorage.getItem('authToken')).toBeNull();
    expect(localStorage.getItem('refreshToken')).toBeNull();
  });
});
