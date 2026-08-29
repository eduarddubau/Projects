import { Directive, ElementRef, inject } from '@angular/core';

/**
 * Makes a row that responds to a click respond to a keyboard too.
 *
 * A `<tr>` with a `(click)` handler is invisible to keyboard and screen-reader users: it
 * takes no focus and fires on no key. Every table in this app had that, and in the trashes
 * the row is the *only* way to reach Restore — WCAG 2.2 SC 2.1.1, and the same standard the
 * board's no-drag menu path was built for.
 *
 * Re-dispatches as a click rather than exposing its own output, so a template keeps the one
 * handler it already had and adds only the attribute.
 */
@Directive({
  selector: '[appClickableRow]',
  host: {
    tabindex: '0',
    role: 'button',
    '(keydown)': 'onKeydown($event)',
  },
})
export class ClickableRowDirective {
  private host = inject<ElementRef<HTMLElement>>(ElementRef);

  onKeydown(event: KeyboardEvent): void {
    if (event.key !== 'Enter' && event.key !== ' ') return;

    // Space scrolls the page otherwise, and Enter can submit a surrounding form.
    event.preventDefault();
    this.host.nativeElement.click();
  }
}
