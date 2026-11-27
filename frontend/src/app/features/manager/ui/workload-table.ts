import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import type { Schemas } from '../../../core/api/api';

/** Every officer, including those with no work, in the API's order (by name). */
@Component({
  selector: 'app-workload-table',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="overflow-x-auto rounded-lg border border-neutral-200 bg-white">
      <table class="min-w-full divide-y divide-neutral-200 text-sm">
        <thead
          class="bg-neutral-50 text-left text-xs font-medium tracking-wide text-neutral-500 uppercase"
        >
          <tr>
            <th class="px-3 py-2">Officer</th>
            <th class="px-3 py-2 text-right">Open claims</th>
            <th class="px-3 py-2 text-right">SLA breaches</th>
            <th class="px-3 py-2 text-right">Settled (7 days)</th>
          </tr>
        </thead>
        <tbody class="divide-y divide-neutral-100">
          @for (officer of officers(); track officer.officerId) {
            <tr>
              <td class="px-3 py-2">{{ officer.name }}</td>
              <td class="px-3 py-2 text-right tabular-nums">{{ officer.openClaims }}</td>
              <td class="px-3 py-2 text-right tabular-nums">
                @if (+officer.slaBreaches > 0) {
                  <span
                    class="rounded-full bg-neutral-900 px-2 py-0.5 text-xs font-medium text-white"
                    >{{ officer.slaBreaches }}</span
                  >
                } @else {
                  0
                }
              </td>
              <td class="px-3 py-2 text-right tabular-nums">{{ officer.settledLast7Days }}</td>
            </tr>
          } @empty {
            <tr>
              <td colspan="4" class="px-3 py-8 text-center text-neutral-500">No officers.</td>
            </tr>
          }
        </tbody>
      </table>
    </div>
  `,
})
export class WorkloadTable {
  readonly officers = input.required<readonly Schemas['OfficerWorkload'][]>();
}
