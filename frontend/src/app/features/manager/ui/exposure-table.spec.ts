import { TestBed } from '@angular/core/testing';
import type { Schemas } from '../../../core/api/api';
import { ExposureTable } from './exposure-table';

describe('ExposureTable', () => {
  it('shows each currency on its own row and never adds them up', async () => {
    const lines: Schemas['ExposureLine'][] = [
      {
        market: 'HongKong',
        productType: 'Motor',
        openClaims: 2,
        exposure: { amount: '1000.50', currency: 'HKD' },
      },
      {
        market: 'HongKong',
        productType: 'Motor',
        openClaims: 1,
        exposure: { amount: '200.00', currency: 'USD' },
      },
    ];
    const fixture = TestBed.createComponent(ExposureTable);
    fixture.componentRef.setInput('lines', lines);
    await fixture.whenStable();

    const rows = [...(fixture.nativeElement as HTMLElement).querySelectorAll('tbody tr')].map(
      (tr) => [...tr.querySelectorAll('td')].map((td) => td.textContent?.trim()),
    );
    expect(rows).toHaveLength(2);
    expect(rows.map((r) => r.slice(0, 4))).toEqual([
      ['Hong Kong', 'Motor', 'HKD', '2'],
      ['Hong Kong', 'Motor', 'USD', '1'],
    ]);
    expect(rows[0]?.[4]).toContain('1,000.50');
    expect(rows[1]?.[4]).toContain('200.00');
    expect((fixture.nativeElement as HTMLElement).textContent).not.toMatch(/total/i);
  });
});
