import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { signal } from '@angular/core';

import { ProfileComponent } from './profile.component';
import { API_URL } from '@core/tokens/app.tokens';
import { Profile } from '@core/models/profile';
import { ThemeService } from '@core/services/theme.service';
import { provideTranslocoTesting } from '@shared/testing/transloco-testing';

const apiUrl = 'http://api.test';

// The real ThemeService touches window.matchMedia, which jsdom lacks.
const themeStub = { provide: ThemeService, useValue: { theme: signal('light'), set: () => {} } };

const sampleProfile: Profile = {
  id: '11111111-1111-1111-1111-111111111111',
  email: 'dev2@example.com',
  firstName: 'Dev',
  lastName: 'User2',
  createdAt: '2026-01-15T10:00:00Z',
};

describe('ProfileComponent', () => {
  let fixture: ComponentFixture<ProfileComponent>;
  let httpMock: HttpTestingController;

  beforeEach(async () => {
    localStorage.clear();
    await TestBed.configureTestingModule({
      imports: [ProfileComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        provideTranslocoTesting(),
        themeStub,
        { provide: API_URL, useValue: apiUrl },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(ProfileComponent);
    httpMock = TestBed.inject(HttpTestingController);
    fixture.detectChanges();
  });

  afterEach(() => httpMock.verify());

  it('renders the loaded profile', () => {
    httpMock.expectOne(`${apiUrl}/profile`).flush(sampleProfile);
    fixture.detectChanges();

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Dev User2');
    expect(text).toContain('dev2@example.com');
  });

  it('shows the error state when loading fails', () => {
    httpMock.expectOne(`${apiUrl}/profile`).flush('boom', { status: 500, statusText: 'Server Error' });
    fixture.detectChanges();

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Failed to load your profile. Please try again.');
  });

  it('saves edited names via PUT and shows the updated profile', () => {
    httpMock.expectOne(`${apiUrl}/profile`).flush(sampleProfile);
    fixture.detectChanges();

    const component = fixture.componentInstance;
    component.startEdit();
    component.form.setValue({ firstName: 'Grace', lastName: 'Hopper' });
    component.save();

    const putReq = httpMock.expectOne(`${apiUrl}/profile`);
    expect(putReq.request.method).toBe('PUT');
    expect(putReq.request.body).toEqual({ firstName: 'Grace', lastName: 'Hopper' });
    putReq.flush({ ...sampleProfile, firstName: 'Grace', lastName: 'Hopper' });
    fixture.detectChanges();

    expect(component.isEditing()).toBe(false);
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Grace Hopper');
  });
});
