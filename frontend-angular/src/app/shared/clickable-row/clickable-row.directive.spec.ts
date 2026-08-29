import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { vi } from 'vitest';

import { ClickableRowDirective } from './clickable-row.directive';

@Component({
  imports: [ClickableRowDirective],
  template: `<tr appClickableRow (click)="activated()"></tr>`,
})
class HostComponent {
  activated = vi.fn();
}

describe('ClickableRowDirective', () => {
  function setup() {
    const fixture = TestBed.createComponent(HostComponent);
    fixture.detectChanges();
    return {
      fixture,
      row: (fixture.nativeElement as HTMLElement).querySelector('tr')!,
    };
  }

  it('puts the row in the tab order and names it as a control', () => {
    const { row } = setup();

    expect(row.getAttribute('tabindex')).toBe('0');
    expect(row.getAttribute('role')).toBe('button');
  });

  // The point of the directive: a row whose only affordance is (click) is unreachable
  // without a pointer, and in the trashes that click is the only route to Restore.
  it.each(['Enter', ' '])('activates the row on %s', (key) => {
    const { fixture, row } = setup();

    row.dispatchEvent(new KeyboardEvent('keydown', { key, bubbles: true }));

    expect(fixture.componentInstance.activated).toHaveBeenCalledTimes(1);
  });

  it('leaves other keys alone', () => {
    const { fixture, row } = setup();

    row.dispatchEvent(new KeyboardEvent('keydown', { key: 'a', bubbles: true }));

    expect(fixture.componentInstance.activated).not.toHaveBeenCalled();
  });

  // Space scrolls the page otherwise, which moves the table out from under the reader.
  it('prevents the default action so Space does not scroll', () => {
    const { row } = setup();
    const event = new KeyboardEvent('keydown', { key: ' ', bubbles: true, cancelable: true });

    row.dispatchEvent(event);

    expect(event.defaultPrevented).toBe(true);
  });
});
