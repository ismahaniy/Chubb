import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import type { Schemas } from '../../core/api/api';

/**
 * Formats one amount in its own currency. The amount is formatted from its string form, so
 * numeric(18,2) values keep their exact digits (Intl.NumberFormat accepts decimal strings; the
 * ES2022 lib typings only declare number | bigint, hence the cast).
 */
@Component({
  selector: 'app-money',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<span class="tabular-nums">{{ formatted() }}</span>`,
})
export class Money {
  readonly value = input.required<Schemas['MoneyDto']>();

  protected readonly formatted = computed(() => {
    const { amount, currency } = this.value();
    return new Intl.NumberFormat(undefined, { style: 'currency', currency }).format(
      String(amount) as unknown as number,
    );
  });
}
