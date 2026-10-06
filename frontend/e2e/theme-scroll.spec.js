import { expect, test } from '@playwright/test';

// Synthetic session and fully intercepted APIs: no real data or cash mutations.
async function mockWorkspace(page, theme = 'dark') {
  const frontendUrl = new URL(test.info().project.use.baseURL);
  expect(['localhost', '127.0.0.1', '[::1]']).toContain(frontendUrl.hostname);
  await page.addInitScript(() => {
    localStorage.setItem('auth-storage', JSON.stringify({ version: 2, state: {
      isAuthenticated: true, tenantId: 3, branchId: 7, token: 'mock-only', refreshToken: 'mock-only',
      user: { id: 5, name: 'Operador UAT', role: 'manager', isPlatformAdmin: false, companyId: 3, branchId: 7, branchName: 'Principal' },
    } }));
  });
  const writes = [];
  await page.route('**/*', async route => {
    const url = new URL(route.request().url());
    if (url.pathname.includes('/api/')) {
      if (route.request().method() !== 'GET') {
        writes.push(url.pathname);
        return route.abort();
      }
      let data = [];
      if (url.pathname.endsWith('/features')) data = ['inventory', 'restaurant', 'cash', 'pos'].map(code => ({ code, isEnabled: true }));
      if (url.pathname.endsWith('/company/settings')) data = { name: 'Prueba POS', themePreference: theme };
      if (url.pathname.endsWith('/cash-register/active')) data = {
        id: 81, status: 'open', openingAmount: 5000, openedBy: 5, openedByName: 'Operador UAT',
        openedAt: new Date().toISOString(), totalSales: 0, totalCashSales: 0, totalCardSales: 0,
        totalTransferSales: 0, cashIn: 0, cashOut: 0, orderCount: 0,
      };
      if (url.pathname.endsWith('/cash-register/status')) data = { branchId: 7, status: 'open' };
      if (url.pathname.endsWith('/inventory/stock')) data = Array.from({ length: 100 }, (_, index) => ({
        productId: index + 1, productName: `Producto ${String(index + 1).padStart(3, '0')}`, sku: `SKU-${index + 1}`,
        category: 'Bebidas', productType: 'simple', stockStatus: 'ok', quantity: 50, unit: 'und', costPrice: 1800, salePrice: 3000,
      }));
      if (url.pathname.endsWith('/sales/tables')) data = [{
        id: 12, tableNumber: 3, name: 'Mesa prueba', total: 3000, createdAt: new Date().toISOString(),
        items: [{ id: 1, productName: 'Producto prueba', quantity: 1, unitPrice: 3000 }],
      }];
      return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ data, count: Array.isArray(data) ? data.length : 0 }) });
    }
    // Only load the chosen local frontend, never external services/assets.
    if (url.origin !== frontendUrl.origin) return route.abort();
    return route.continue();
  });
  return writes;
}

function contrast(foreground, background) {
  const luminance = color => {
    const values = color.match(/[\d.]+/g).slice(0, 3).map(Number).map(value => {
      const c = value / 255;
      return c <= 0.04045 ? c / 12.92 : ((c + 0.055) / 1.055) ** 2.4;
    });
    return values[0] * 0.2126 + values[1] * 0.7152 + values[2] * 0.0722;
  };
  const a = luminance(foreground), b = luminance(background);
  return (Math.max(a, b) + 0.05) / (Math.min(a, b) + 0.05);
}

for (const theme of ['dark', 'light']) {
  test(`cash amount/notes have readable typed and placeholder text in ${theme}`, async ({ page }) => {
    const writes = await mockWorkspace(page, theme);
    await page.goto('/cash');
    await page.getByRole('button', { name: 'Cerrar Caja', exact: true }).click();
    const amount = page.getByRole('spinbutton', { name: 'Conteo real de efectivo' });
    const notes = page.getByRole('textbox', { name: 'Notas de cierre (opcional)' });
    await expect(page.locator('html')).toHaveAttribute('data-theme', theme);
    await amount.fill('5000');
    await notes.fill('Arqueo confirmado');
    for (const field of [amount, notes]) {
      const colors = await field.evaluate(el => ({ color: getComputedStyle(el).color, background: getComputedStyle(el).backgroundColor, placeholder: getComputedStyle(el, '::placeholder').color }));
      expect(contrast(colors.color, colors.background)).toBeGreaterThanOrEqual(4.5);
      expect(contrast(colors.placeholder, colors.background)).toBeGreaterThanOrEqual(4.5);
    }
    await expect(page.getByText('Cuadra perfectamente')).toBeVisible();
    await test.info().attach(`cash-${theme}`, { body: await page.screenshot(), contentType: 'image/png' });
    await page.getByRole('button', { name: 'Cancelar', exact: true }).click();
    expect(writes).toEqual([]);
  });
}

test('invoice panel receives the shared thin hover/focus scrollbar without a local class', async ({ page }) => {
  await page.setViewportSize({ width: 1280, height: 600 });
  const writes = await mockWorkspace(page);
  await page.goto('/sales');
  await page.getByRole('button', { name: 'Facturar', exact: true }).click();
  const panel = page.locator('.fixed').filter({ has: page.getByText('Producto prueba', { exact: true }) }).locator('.overflow-y-auto');
  await expect(panel).toHaveCount(1);
  await expect(panel).toBeVisible();
  const style = () => panel.evaluate(el => ({ width: getComputedStyle(el, '::-webkit-scrollbar').width, track: getComputedStyle(el, '::-webkit-scrollbar-track').backgroundColor, thumb: getComputedStyle(el, '::-webkit-scrollbar-thumb').backgroundColor, height: el.clientHeight, total: el.scrollHeight, top: el.scrollTop }));
  await page.mouse.move(0, 0);
  await page.evaluate(() => document.activeElement?.blur());
  const idle = await style();
  expect(idle.width).toBe('6px');
  expect(idle.track).toBe('rgba(0, 0, 0, 0)');
  expect(idle.thumb).toBe('rgba(0, 0, 0, 0)');
  expect(idle.total).toBeGreaterThan(idle.height);
  await panel.hover();
  expect((await style()).thumb).not.toBe(idle.thumb);
  await page.mouse.wheel(0, 300);
  await expect.poll(async () => (await style()).top).toBeGreaterThan(0);
  await panel.locator('input').first().focus();
  await page.mouse.move(0, 0);
  expect((await style()).thumb).not.toBe(idle.thumb);
  await test.info().attach('invoice-scroll-dark', { body: await page.screenshot(), contentType: 'image/png' });
  expect(writes).toEqual([]);
});

test('inventory keeps its bounded internal scroll and sticky headings after centralizing styles', async ({ page }) => {
  await page.setViewportSize({ width: 1280, height: 720 });
  const writes = await mockWorkspace(page);
  await page.goto('/inventory');
  const viewport = page.getByRole('region', { name: 'Tabla de inventario', exact: true });
  await expect(viewport).toBeVisible();
  await viewport.hover();
  await page.mouse.wheel(0, 400);
  await expect.poll(() => viewport.evaluate(el => el.scrollTop)).toBeGreaterThan(0);
  const geometry = await viewport.evaluate(el => ({
    top: el.getBoundingClientRect().top, headTop: el.querySelector('th').getBoundingClientRect().top,
    documentHeight: document.documentElement.scrollHeight, windowHeight: innerHeight,
    mainScroll: document.querySelector('main').scrollTop, bodyScroll: document.documentElement.scrollTop,
    bar: getComputedStyle(el, '::-webkit-scrollbar').width,
  }));
  expect(geometry.bar).toBe('6px');
  expect(geometry.documentHeight).toBe(geometry.windowHeight);
  expect(geometry.mainScroll + geometry.bodyScroll).toBe(0);
  expect(Math.abs(geometry.top - geometry.headTop)).toBeLessThan(2);
  expect(writes).toEqual([]);
});

test('shared defaults preserve explicit hidden scrollbars and touch/high-contrast visibility', async ({ page, browser }) => {
  await mockWorkspace(page);
  await page.goto('/cash');
  await expect(page.getByRole('heading', { name: 'Caja', exact: true })).toBeVisible();
  await page.evaluate(() => {
    for (const name of ['plain', 'scrollbar-subtle', 'inventory-table-scroll', 'scrollbar-hide']) {
      const el = document.createElement('div');
      el.dataset.scrollProbe = name;
      el.className = name;
      el.tabIndex = 0;
      el.style.cssText = 'position:fixed;top:80px;left:400px;width:100px;height:100px;overflow:auto';
      el.innerHTML = '<div style="height:1000px;width:1000px">Scroll probe</div>';
      document.body.append(el);
    }
  });
  await page.mouse.move(0, 0);
  for (const name of ['plain', 'scrollbar-subtle', 'inventory-table-scroll']) {
    const probe = page.locator(`[data-scroll-probe="${name}"]`);
    expect(await probe.evaluate(el => getComputedStyle(el, '::-webkit-scrollbar').width)).toBe('6px');
    expect(await probe.evaluate(el => getComputedStyle(el, '::-webkit-scrollbar-thumb').backgroundColor)).toBe('rgba(0, 0, 0, 0)');
  }
  const hidden = page.locator('[data-scroll-probe="scrollbar-hide"]');
  expect(await hidden.evaluate(el => getComputedStyle(el, '::-webkit-scrollbar').display)).toBe('none');
  await page.emulateMedia({ forcedColors: 'active' });
  const plain = page.locator('[data-scroll-probe="plain"]');
  expect(await plain.evaluate(el => getComputedStyle(el, '::-webkit-scrollbar-thumb').backgroundColor)).not.toBe('rgba(0, 0, 0, 0)');
  const touch = await browser.newPage({ hasTouch: true, isMobile: true, viewport: { width: 390, height: 844 } });
  try {
    await mockWorkspace(touch);
    await touch.goto('/cash');
    await expect(touch.getByRole('heading', { name: 'Caja', exact: true })).toBeVisible();
    expect(await touch.locator('main').last().evaluate(el => getComputedStyle(el, '::-webkit-scrollbar-thumb').backgroundColor)).not.toBe('rgba(0, 0, 0, 0)');
  } finally {
    await touch.close();
  }
});
