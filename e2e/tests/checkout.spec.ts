import { expect, test } from '@playwright/test';

async function placeOrder(page, quantity: string, promo = '') {
  await page.goto('/');
  await page.getByLabel('Quantity for Rank coaching session').fill(quantity);
  await page.getByLabel('Email').fill('buyer@example.com');
  if (promo) {
    await page.getByLabel('Promo code').fill(promo);
  }
  await page.getByRole('button', { name: 'Place order' }).click();
  await expect(page.getByTestId('status')).toHaveText('Pending');
}

test('a customer can place an order and pay for it', async ({ page }) => {
  await placeOrder(page, '2');
  await expect(page.getByTestId('subtotal')).toHaveText('$50.00');
  await expect(page.getByTestId('tax')).toHaveText('$6.50');
  await expect(page.getByTestId('total')).toHaveText('$56.50');

  await page.getByRole('button', { name: 'Pay now' }).click();
  await expect(page.getByTestId('status')).toHaveText('Paid');
  await expect(page.getByTestId('receipt')).toContainText('pay_');
});

test('a promo code takes ten percent off', async ({ page }) => {
  await placeOrder(page, '1', 'WELCOME10');
  await expect(page.getByTestId('discount')).toHaveText('$2.50');
  await expect(page.getByTestId('total')).toHaveText('$25.43');
});

test('a declined card shows an error and leaves the order unpaid', async ({ page }) => {
  await placeOrder(page, '1');
  await page.getByLabel('Card').selectOption('tok_declined');
  await page.getByRole('button', { name: 'Pay now' }).click();
  await expect(page.getByRole('alert')).toHaveText('The card was declined.');
  await expect(page.getByTestId('status')).toHaveText('Pending');
});

test('an invalid email is rejected with a clear message', async ({ page }) => {
  await page.goto('/');
  await page.getByLabel('Quantity for Replay review').fill('1');
  await page.getByLabel('Email').fill('not-an-email');
  await page.getByRole('button', { name: 'Place order' }).click();
  await expect(page.getByRole('alert')).toHaveText('Enter a valid email address.');
});

test('the order button stays disabled until something is selected', async ({ page }) => {
  await page.goto('/');
  await expect(page.getByRole('button', { name: 'Place order' })).toBeDisabled();
});
