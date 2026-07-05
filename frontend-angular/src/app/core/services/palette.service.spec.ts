import { TestBed } from '@angular/core/testing';
import { PaletteService } from './palette.service';

describe('PaletteService', () => {
  beforeEach(() => {
    localStorage.clear();
    document.documentElement.removeAttribute('data-palette');
  });

  function create(): PaletteService {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({});
    return TestBed.inject(PaletteService);
  }

  it('defaults to violet with no attribute', () => {
    const service = create();
    expect(service.palette()).toBe('violet');
    expect(document.documentElement.hasAttribute('data-palette')).toBe(false);
  });

  it('restores a stored palette on init', () => {
    localStorage.setItem('palette', 'indigo');
    const service = create();
    expect(service.palette()).toBe('indigo');
  });

  it('ignores an unknown stored palette', () => {
    localStorage.setItem('palette', 'chartreuse');
    const service = create();
    expect(service.palette()).toBe('violet');
  });

  it('set() applies the data-palette attribute and persists', () => {
    const service = create();

    service.set('emerald');

    expect(service.palette()).toBe('emerald');
    expect(document.documentElement.dataset['palette']).toBe('emerald');
    expect(localStorage.getItem('palette')).toBe('emerald');
  });

  it('set("violet") clears the attribute (mat.theme default)', () => {
    const service = create();

    service.set('rose');
    service.set('violet');

    expect(document.documentElement.hasAttribute('data-palette')).toBe(false);
    expect(localStorage.getItem('palette')).toBe('violet');
  });
});
