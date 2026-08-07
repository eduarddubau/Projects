import { TestBed } from '@angular/core/testing';
import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Observable, of, throwError } from 'rxjs';
import { authInterceptor } from './auth.interceptor';
import { AuthService } from '@core/services/auth.service';
import { API_URL } from '@core/tokens/app.tokens';

describe('authInterceptor', () => {
  let http: HttpClient;
  let httpMock: HttpTestingController;
  let auth: {
    getToken: () => string | null;
    getRefreshToken: () => string | null;
    refresh: () => Observable<string>;
    logout: () => void;
    loggedOut: boolean;
  };

  beforeEach(() => {
    auth = {
      getToken: () => 'access-1',
      getRefreshToken: () => 'refresh-1',
      refresh: () => of('access-2'),
      logout() {
        this.loggedOut = true;
      },
      loggedOut: false,
    };
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
        { provide: AuthService, useValue: auth },
        { provide: API_URL, useValue: '/api' },
      ],
    });
    http = TestBed.inject(HttpClient);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('attaches the access token as a Bearer header', () => {
    http.get('/api/data').subscribe();

    const req = httpMock.expectOne('/api/data');
    expect(req.request.headers.get('Authorization')).toBe('Bearer access-1');
    req.flush({});
  });

  it('refreshes and replays the request on 401', () => {
    let succeeded = false;
    http.get('/api/data').subscribe(() => (succeeded = true));

    httpMock
      .expectOne('/api/data')
      .flush('unauthorized', { status: 401, statusText: 'Unauthorized' });

    const retry = httpMock.expectOne('/api/data');
    expect(retry.request.headers.get('Authorization')).toBe('Bearer access-2');
    retry.flush({});

    expect(succeeded).toBe(true);
  });

  it('does not refresh on a 401 from an auth endpoint', () => {
    let errored = false;
    http.post('/api/auth/login', {}).subscribe({ error: () => (errored = true) });

    httpMock.expectOne('/api/auth/login').flush('bad', { status: 401, statusText: 'Unauthorized' });

    httpMock.expectNone('/api/auth/login'); // no retry
    expect(errored).toBe(true);
  });

  it('does not refresh when there is no refresh token', () => {
    auth.getRefreshToken = () => null;
    let errored = false;
    http.get('/api/data').subscribe({ error: () => (errored = true) });

    httpMock
      .expectOne('/api/data')
      .flush('unauthorized', { status: 401, statusText: 'Unauthorized' });

    httpMock.expectNone('/api/data'); // no retry
    expect(errored).toBe(true);
  });

  it('logs out when the refresh itself fails', () => {
    auth.refresh = () => throwError(() => new Error('refresh failed'));
    let errored = false;
    http.get('/api/data').subscribe({ error: () => (errored = true) });

    httpMock
      .expectOne('/api/data')
      .flush('unauthorized', { status: 401, statusText: 'Unauthorized' });

    expect(auth.loggedOut).toBe(true);
    expect(errored).toBe(true);
  });

  it('never leaks the token to third-party hosts', () => {
    http.get('https://ipwho.is/').subscribe();

    const req = httpMock.expectOne('https://ipwho.is/');
    expect(req.request.headers.has('Authorization')).toBe(false);
    req.flush({});
  });
});
