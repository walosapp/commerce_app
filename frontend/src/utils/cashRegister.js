export const calculateExpectedCash = ({
  openingAmount,
  totalCashSales,
  cashIn,
  cashOut,
}) => openingAmount + totalCashSales + cashIn - cashOut;
