import { TestBed } from '@angular/core/testing';
import type { Schemas } from '../../../core/api/api';
import { ClaimForm } from './claim-form';

const POLICIES: Schemas['PolicySummary'][] = [
  {
    policyNumber: 'POL-SG-0001',
    market: 'Singapore',
    productType: 'Motor',
    currency: 'SGD',
    maxPayable: { amount: 49500, currency: 'SGD' },
    startDate: '2026-01-01',
    endDate: '2026-12-31',
  },
  {
    policyNumber: 'POL-MY-0002',
    market: 'Malaysia',
    productType: 'Property',
    currency: 'MYR',
    maxPayable: { amount: 497500, currency: 'MYR' },
    startDate: '2026-01-01',
    endDate: '2026-12-31',
  },
];

describe('ClaimForm', () => {
  async function render() {
    const fixture = TestBed.createComponent(ClaimForm);
    fixture.componentRef.setInput('policies', POLICIES);
    const emitted: Schemas['SubmitClaimRequest'][] = [];
    fixture.componentInstance.submitClaim.subscribe((r) => emitted.push(r));
    await fixture.whenStable();
    const el = fixture.nativeElement as HTMLElement;

    const set = async (selector: string, value: string) => {
      const input = el.querySelector<HTMLInputElement>(selector)!;
      input.value = value;
      input.dispatchEvent(new Event(input.tagName === 'SELECT' ? 'change' : 'input'));
      await fixture.whenStable();
    };
    const button = () => el.querySelector<HTMLButtonElement>('button[type=submit]')!;
    const submit = async () => {
      el.querySelector('form')!.dispatchEvent(new Event('submit'));
      await fixture.whenStable();
    };
    return { el, set, button, submit, emitted };
  }

  async function fillValid(form: Awaited<ReturnType<typeof render>>) {
    await form.set('select', 'POL-MY-0002');
    await form.set('input[type=date]', '2026-11-20');
    await form.set('input[type=number]', '1500.5');
    await form.set('textarea', '  Burst pipe flooded the kitchen.  ');
  }

  it('stays disabled until every field is valid', async () => {
    const form = await render();
    expect(form.button().disabled).toBe(true);

    await fillValid(form);
    expect(form.button().disabled).toBe(false);

    await form.set('textarea', '   ');
    expect(form.button().disabled).toBe(true);
  });

  it('rejects an amount of zero', async () => {
    const form = await render();
    await fillValid(form);
    await form.set('input[type=number]', '0');

    expect(form.button().disabled).toBe(true);
    await form.submit();
    expect(form.emitted).toEqual([]);
  });

  it('takes product type and currency from the chosen policy and trims the description', async () => {
    const form = await render();
    await fillValid(form);
    expect(form.el.textContent).toContain('Amount claimed (MYR)');

    await form.submit();

    expect(form.emitted).toEqual([
      {
        policyNumber: 'POL-MY-0002',
        productType: 'Property',
        currency: 'MYR',
        incidentDate: '2026-11-20',
        claimedAmount: 1500.5,
        description: 'Burst pipe flooded the kitchen.',
      },
    ]);
  });
});
