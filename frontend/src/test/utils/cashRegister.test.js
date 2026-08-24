import { describe, expect, it } from 'vitest';
import { calculateExpectedCash } from '../../utils/cashRegister';

describe('calculateExpectedCash', () => {
  it('sums opening cash, cash sales and cash in, then subtracts cash out', () => {
    expect(calculateExpectedCash({
      openingAmount: 100,
      totalCashSales: 250,
      cashIn: 50,
      cashOut: 25,
    })).toBe(375);
  });

  it('does not subtract granted credits', () => {
    expect(calculateExpectedCash({
      openingAmount: 100,
      totalCashSales: 250,
      cashIn: 50,
      cashOut: 25,
      totalCredits: 200,
    })).toBe(375);
  });
});
