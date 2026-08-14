import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { vi } from 'vitest';

import { ConfirmDialogComponent, ConfirmDialogData } from './confirm-dialog.component';
import { provideTranslocoTesting } from '@shared/testing/transloco-testing';

describe('ConfirmDialogComponent', () => {
  let fixture: ComponentFixture<ConfirmDialogComponent>;
  let dialogRef: { close: ReturnType<typeof vi.fn> };

  async function setup(data: Partial<ConfirmDialogData> = {}) {
    dialogRef = { close: vi.fn() };

    await TestBed.configureTestingModule({
      imports: [ConfirmDialogComponent],
      providers: [
        provideTranslocoTesting(),
        { provide: MatDialogRef, useValue: dialogRef },
        {
          provide: MAT_DIALOG_DATA,
          useValue: { title: 'Delete this?', message: 'Gone for good.', ...data },
        },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(ConfirmDialogComponent);
    fixture.detectChanges();
  }

  function confirmButton(): HTMLButtonElement {
    const buttons = (fixture.nativeElement as HTMLElement).querySelectorAll('button');
    return buttons[buttons.length - 1] as HTMLButtonElement;
  }

  function phraseInput(): HTMLInputElement | null {
    return (fixture.nativeElement as HTMLElement).querySelector('input');
  }

  // Pins the behaviour the seven callers that predate confirmPhrase rely on.
  describe('without a phrase', () => {
    it('renders no input and confirms on the first click', async () => {
      await setup();

      expect(phraseInput()).toBeNull();
      expect(confirmButton().disabled).toBe(false);

      fixture.componentInstance.confirm();
      expect(dialogRef.close).toHaveBeenCalledWith(true);
    });

    it('still cancels with false', async () => {
      await setup();

      fixture.componentInstance.cancel();
      expect(dialogRef.close).toHaveBeenCalledWith(false);
    });
  });

  describe('with a phrase', () => {
    it('starts locked and renders the input', async () => {
      await setup({ confirmPhrase: 'Acme Team', confirmPhraseLabel: 'Workspace name' });

      expect(phraseInput()).not.toBeNull();
      expect(confirmButton().disabled).toBe(true);
    });

    it('unlocks once the phrase matches', async () => {
      await setup({ confirmPhrase: 'Acme Team', confirmPhraseLabel: 'Workspace name' });

      fixture.componentInstance.typed.set('Acme Team');
      fixture.detectChanges();

      expect(confirmButton().disabled).toBe(false);
    });

    it('forgives surrounding whitespace but not a different capitalisation', async () => {
      await setup({ confirmPhrase: 'Acme Team' });

      fixture.componentInstance.typed.set('  Acme Team  ');
      expect(fixture.componentInstance.canConfirm()).toBe(true);

      fixture.componentInstance.typed.set('acme team');
      expect(fixture.componentInstance.canConfirm()).toBe(false);
    });

    it('refuses to close on a near miss even when called directly', async () => {
      await setup({ confirmPhrase: 'Acme Team' });

      fixture.componentInstance.typed.set('Acme Tea');
      fixture.componentInstance.confirm();

      expect(dialogRef.close).not.toHaveBeenCalled();
    });

    it('reads what the user actually types', async () => {
      await setup({ confirmPhrase: 'Acme Team', confirmPhraseLabel: 'Workspace name' });

      const input = phraseInput()!;
      input.value = 'Acme Team';
      input.dispatchEvent(new Event('input'));
      fixture.detectChanges();

      expect(confirmButton().disabled).toBe(false);
    });
  });
});
