import { Pipe, PipeTransform } from '@angular/core';
import type { Schemas } from '../../core/api/api';

type Market = Schemas['Market'];

/** Display names per market. `Record<Market, …>` makes the compiler flag a market added to the API. */
export const MARKET_LABELS: Record<Market, string> = {
  Singapore: 'Singapore',
  HongKong: 'Hong Kong',
  Malaysia: 'Malaysia',
  Thailand: 'Thailand',
  Indonesia: 'Indonesia',
  Philippines: 'Philippines',
};

/** `{{ claim.market | marketLabel }}` → "Hong Kong". */
@Pipe({ name: 'marketLabel' })
export class MarketLabelPipe implements PipeTransform {
  transform(market: Market): string {
    return MARKET_LABELS[market];
  }
}
