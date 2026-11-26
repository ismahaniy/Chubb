import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';
import type { CurrentUser } from '../../core/api/api';

export interface NavLink {
  label: string;
  path: string;
}

@Component({
  selector: 'app-nav',
  imports: [RouterLink, RouterLinkActive],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <nav class="flex items-center gap-6">
      <ul class="flex gap-4 text-sm">
        @for (link of links(); track link.path) {
          <li>
            <a
              [routerLink]="link.path"
              routerLinkActive="text-neutral-900 font-medium"
              class="text-neutral-500 hover:text-neutral-900"
              >{{ link.label }}</a
            >
          </li>
        }
      </ul>
      <div class="flex items-center gap-3 border-l border-neutral-200 pl-6 text-sm">
        <span class="text-neutral-700">{{ user().displayName }}</span>
        <span class="rounded bg-neutral-100 px-2 py-0.5 text-xs text-neutral-600">{{
          user().role
        }}</span>
        <button
          type="button"
          class="text-neutral-500 hover:text-neutral-900"
          (click)="signOut.emit()"
        >
          Sign out
        </button>
      </div>
    </nav>
  `,
})
export class AppNav {
  readonly user = input.required<CurrentUser>();
  readonly links = input.required<readonly NavLink[]>();
  readonly signOut = output();
}
