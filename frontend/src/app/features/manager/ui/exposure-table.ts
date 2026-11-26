import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import type { Schemas } from '../../../core/api/api';
import { MarketLabelPipe } from '../../../shared/ui/markets';
import { Money } from '../../../shared/ui/money';

/**
 * Outstanding liability, one row per market, product and currency, in the API's order. There is deliberately no
 * total: amounts in different currencies are never added together.
 */
@Component({
  selector: 'app-exposure-table',
  imports: [MarketLabelPipe, Money],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="overflow-x-auto rounded-lg border border-neutral-200 bg-white">
      <table class="min-w-full divide-y divide-neutral-200 text-sm">
        <thead
          class="bg-neutral-50 text-left text-xs font-medium tracking-wide text-neutral-500 uppercase"
        >
          <tr>
            <th class="px-3 py-2">Market</th>
            <th class="px-3 py-2">Product</th>
            <th class="px-3 py-2">Currency</th>
            <th class="px-3 py-2 text-right">Open claims</th>
            <th class="px-3 py-2 text-right">Exposure</th>
          </tr>
        </thead>
        <tbody class="divide-y divide-neutral-100">
          @for (line of lines(); track line.market + line.productType + line.exposure.currency) {
            <tr>
              <td class="px-3 py-2">{{ line.market | marketLabel }}</td>
              <td class="px-3 py-2">{{ line.productType }}</td>
              <td class="px-3 py-2">{{ line.exposure.currency }}</td>
              <td class="px-3 py-2 text-right tabular-nums">{{ line.openClaims }}</td>
              <td class="px-3 py-2 text-right"><app-money [value]="line.exposure" /></td>
            </tr>
          } @empty {
            <tr>
              <td colspan="5" class="px-3 py-8 text-center text-neutral-500">No open claims.</td>
            </tr>
          }
        </tbody>
      </table>
    </div>
  `,
})
export class ExposureTable {
  readonly lines = input.required<readonly Schemas['ExposureLine'][]>();
}
