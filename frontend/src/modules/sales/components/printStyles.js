export const thermalReceiptStyles = `
  @page { size: 80mm auto; margin: 0; }
  * { box-sizing: border-box; margin: 0; padding: 0; }
  body {
    font-family: 'Courier New', 'Lucida Console', monospace;
    font-size: 12px;
    width: 80mm;
    max-width: 80mm;
    margin: 0 auto;
    padding: 3mm;
    color: #000;
    background: #fff;
  }
  .receipt-header { text-align: center; margin-bottom: 6px; }
  .receipt-header h2 { font-size: 16px; margin-bottom: 2px; }
  .receipt-header p { font-size: 11px; }
  .receipt-divider { border-top: 1px dashed #000; margin: 5px 0; }
  .receipt-meta { font-size: 11px; margin-bottom: 4px; }
  .receipt-meta-row { display: flex; justify-content: space-between; }
  .receipt-items { width: 100%; border-collapse: collapse; font-size: 11px; }
  .receipt-items th { text-align: left; border-bottom: 1px solid #000; padding: 2px 0; }
  .receipt-items th:last-child,
  .receipt-items td:last-child { text-align: right; }
  .receipt-items th:nth-child(2),
  .receipt-items td:nth-child(2) { text-align: center; width: 30px; }
  .receipt-items td { padding: 2px 0; }
  .receipt-totals { margin-top: 4px; font-size: 12px; }
  .receipt-total-row { display: flex; justify-content: space-between; padding: 1px 0; }
  .receipt-total-row.grand { font-size: 14px; font-weight: bold; border-top: 1px dashed #000; padding-top: 4px; margin-top: 4px; }
  .receipt-payments { margin-top: 4px; font-size: 11px; }
  .receipt-footer { text-align: center; margin-top: 8px; font-size: 11px; }
`;

export const thermalKitchenStyles = `
  @page { size: 80mm auto; margin: 0; }
  * { box-sizing: border-box; margin: 0; padding: 0; }
  body {
    font-family: 'Courier New', 'Lucida Console', monospace;
    font-size: 16px;
    width: 80mm;
    max-width: 80mm;
    margin: 0 auto;
    padding: 3mm;
    color: #000;
    background: #fff;
  }
  .kitchen-header { text-align: center; font-size: 20px; font-weight: bold; margin-bottom: 4px; }
  .kitchen-meta { text-align: center; font-size: 14px; margin-bottom: 6px; }
  .kitchen-divider { border-top: 2px dashed #000; margin: 6px 0; }
  .kitchen-item { font-size: 18px; font-weight: bold; padding: 4px 0; }
  .kitchen-note { font-size: 14px; color: #333; padding-left: 12px; font-style: italic; }
`;

export const zReportStyles = `
  @page { size: 80mm auto; margin: 0; }
  * { box-sizing: border-box; margin: 0; padding: 0; }
  body {
    font-family: 'Courier New', 'Lucida Console', monospace;
    font-size: 11px;
    width: 80mm;
    max-width: 80mm;
    margin: 0 auto;
    padding: 3mm;
    color: #000;
    background: #fff;
  }
  .z-header { text-align: center; margin-bottom: 6px; }
  .z-header h2 { font-size: 16px; }
  .z-header p { font-size: 10px; }
  .z-divider { border-top: 1px dashed #000; margin: 5px 0; }
  .z-section-title { font-weight: bold; font-size: 12px; margin: 4px 0 2px; }
  .z-row { display: flex; justify-content: space-between; padding: 1px 0; }
  .z-row.total { font-weight: bold; font-size: 13px; }
  .z-movements { margin-top: 4px; }
  .z-movement { padding: 2px 0; font-size: 10px; }
`;

export function openPrintWindow(htmlContent, styles) {
  const printWindow = window.open('', '_blank', 'width=350,height=600');
  if (!printWindow) return;

  printWindow.document.write(`
    <!DOCTYPE html>
    <html><head>
      <meta charset="utf-8">
      <title>Imprimir</title>
      <style>${styles}</style>
    </head><body>${htmlContent}</body></html>
  `);
  printWindow.document.close();
  printWindow.onload = () => {
    printWindow.focus();
    printWindow.print();
    setTimeout(() => printWindow.close(), 1000);
  };
}
