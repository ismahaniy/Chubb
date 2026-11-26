import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import type { Role } from '../../../core/api/api';
import type { DemoUser } from '../demo-users';

@Component({
  selector: 'app-demo-user-list',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="space-y-4">
      @for (group of groups(); track group.role) {
        <section>
          <h3 class="mb-1 text-xs font-medium tracking-wide text-neutral-500 uppercase">
            {{ group.role }}
          </h3>
          <ul class="divide-y divide-neutral-100 rounded-md border border-neutral-200 bg-white">
            @for (user of group.users; track user.email) {
              <li>
                <button
                  type="button"
                  class="flex w-full items-center justify-between px-3 py-2 text-left text-sm hover:bg-neutral-50"
                  (click)="pick.emit(user)"
                >
                  <span class="text-neutral-900">{{ user.displayName }}</span>
                  <span class="text-neutral-500">{{ user.email }}</span>
                </button>
              </li>
            }
          </ul>
        </section>
      }
    </div>
  `,
})
export class DemoUserList {
  readonly users = input.required<readonly DemoUser[]>();
  readonly pick = output<DemoUser>();

  protected readonly groups = computed(() => {
    const byRole = new Map<Role, DemoUser[]>();
    for (const user of this.users()) {
      byRole.set(user.role, [...(byRole.get(user.role) ?? []), user]);
    }
    return [...byRole].map(([role, users]) => ({ role, users }));
  });
}
