import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import type { Schemas } from '../../../core/api/api';
import { MarketLabelPipe } from '../../../shared/ui/markets';
import { Money } from '../../../shared/ui/money';
import { StatusBadge } from '../../../shared/ui/status-badge';

@Component({
  selector: 'app-claim-list',
  imports: [DatePipe, MarketLabelPipe, Money, StatusBadge],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="overflow-x-auto rounded-lg border border-neutral-200 bg-white">
      <table class="min-w-full divide-y divide-neutral-200 text-sm">
        <thead
          class="bg-neutral-50 text-left text-xs font-medium tracking-wide text-neutral-500 uppercase"
        >
          <tr>
            <th class="px-3 py-2">Claim</th>
            <th class="px-3 py-2">Market</th>
            <th class="px-3 py-2">Product</th>
            <th class="px-3 py-2">Status</th>
            <th class="px-3 py-2 text-right">Claimed</th>
            <th class="px-3 py-2">Submitted</th>
            <th class="px-3 py-2"><span class="sr-only">Action</span></th>
          </tr>
        </thead>
        <tbody class="divide-y divide-neutral-100">
          @for (item of items(); track item.id) {
            <tr class="hover:bg-neutral-50">
              <td class="px-3 py-2">
                <div class="font-medium text-neutral-900">{{ item.claimNumber }}</div>
                <div class="text-xs text-neutral-500">{{ item.policyNumber }}</div>
              </td>
              <td class="px-3 py-2">{{ item.market | marketLabel }}</td>
              <td class="px-3 py-2">{{ item.productType }}</td>
              <td class="px-3 py-2 whitespace-nowrap">
                <app-status-badge [status]="item.status" />
                @if (item.status === 'AwaitingInformation') {
                  <span class="ml-2 text-xs font-medium text-neutral-900">Action needed</span>
                }
              </td>
              <td class="px-3 py-2 text-right"><app-money [value]="item.claimedAmount" /></td>
              <td class="px-3 py-2 whitespace-nowrap">
                {{ item.submittedAt | date: 'mediumDate' }}
              </td>
              <td class="px-3 py-2 text-right">
                <button
                  type="button"
                  class="rounded-md border border-neutral-300 px-3 py-1 text-xs font-medium hover:bg-neutral-100"
                  (click)="open.emit(item.id)"
                >
                  {{ item.status === 'AwaitingInformation' ? 'Answer' : 'View' }}
                </button>
              </td>
            </tr>
          } @empty {
            <tr>
              <td colspan="7" class="px-3 py-8 text-center text-neutral-500">
                You have not reported any claims yet.
              </td>
            </tr>
          }
        </tbody>
      </table>
    </div>
  `,
})
export class ClaimList {
  readonly items = input.required<readonly Schemas['ClaimSummary'][]>();
  readonly open = output<string>();
}
