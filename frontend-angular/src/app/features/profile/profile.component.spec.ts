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
  nickname: null,
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

  it('renders the loaded profile', async () => {
    httpMock.expectOne(`${apiUrl}/profile`).flush(sampleProfile);
    await fixture.whenStable();

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Dev User2');
    expect(text).toContain('dev2@example.com');
  });

  it('shows the error state when loading fails', async () => {
    httpMock
      .expectOne(`${apiUrl}/profile`)
      .flush('boom', { status: 500, statusText: 'Server Error' });
    await fixture.whenStable();

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Failed to load your profile. Please try again.');
  });

  it('saves edited names via PUT and shows the updated profile', async () => {
    httpMock.expectOne(`${apiUrl}/profile`).flush(sampleProfile);
    await fixture.whenStable();

    const component = fixture.componentInstance;
    component.startEdit();
    component.form.setValue({
      firstName: 'Grace',
      lastName: 'Hopper',
      email: sampleProfile.email,
      nickname: '',
    });
    component.save();

    const putReq = httpMock.expectOne(`${apiUrl}/profile`);
    expect(putReq.request.method).toBe('PUT');
    expect(putReq.request.body).toEqual({
      firstName: 'Grace',
      lastName: 'Hopper',
      email: sampleProfile.email,
      nickname: null,
    });
    putReq.flush({ ...sampleProfile, firstName: 'Grace', lastName: 'Hopper' });
    await fixture.whenStable();

    expect(component.isEditing()).toBe(false);
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Grace Hopper');
  });

  it('trims the nickname before sending it', async () => {
    httpMock.expectOne(`${apiUrl}/profile`).flush(sampleProfile);
    await fixture.whenStable();

    const component = fixture.componentInstance;
    component.startEdit();
    component.form.setValue({
      firstName: 'Dev',
      lastName: 'User2',
      email: sampleProfile.email,
      nickname: '  Eddy  ',
    });
    component.save();

    const putReq = httpMock.expectOne(`${apiUrl}/profile`);
    expect(putReq.request.body.nickname).toBe('Eddy');
    putReq.flush({ ...sampleProfile, nickname: 'Eddy' });
  });

  // Whitespace has to clear the nickname, not store a blank string: the column
  // is nullable and the backend assigns whatever it is handed.
  it('sends null when the nickname is only whitespace', async () => {
    httpMock.expectOne(`${apiUrl}/profile`).flush({ ...sampleProfile, nickname: 'Eddy' });
    await fixture.whenStable();

    const component = fixture.componentInstance;
    component.startEdit();
    component.form.setValue({
      firstName: 'Dev',
      lastName: 'User2',
      email: sampleProfile.email,
      nickname: '   ',
    });
    component.save();

    const putReq = httpMock.expectOne(`${apiUrl}/profile`);
    expect(putReq.request.body.nickname).toBeNull();
    putReq.flush(sampleProfile);
  });

  it('seeds the edit form with the existing nickname', async () => {
    httpMock.expectOne(`${apiUrl}/profile`).flush({ ...sampleProfile, nickname: 'Eddy' });
    await fixture.whenStable();

    const component = fixture.componentInstance;
    component.startEdit();

    expect(component.form.controls.nickname.value).toBe('Eddy');
  });
});
