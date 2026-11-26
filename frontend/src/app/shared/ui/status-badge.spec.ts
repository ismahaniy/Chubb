import { TestBed } from '@angular/core/testing';
import { StatusBadge } from './status-badge';

describe('StatusBadge', () => {
  it('shows a readable label and follows input changes', async () => {
    const fixture = TestBed.createComponent(StatusBadge);
    fixture.componentRef.setInput('status', 'AwaitingInformation');
    await fixture.whenStable();
    const el = fixture.nativeElement as HTMLElement;
    expect(el.textContent?.trim()).toBe('Awaiting information');

    fixture.componentRef.setInput('status', 'UnderReview');
    await fixture.whenStable();
    expect(el.textContent?.trim()).toBe('Under review');
  });
});
