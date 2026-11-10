const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { chromium } = require(process.env.PLAYWRIGHT_MODULE || 'playwright');

const base = 'http://127.0.0.1:5192';
const out = path.resolve('artifacts/error-page');

(async () => {
    fs.mkdirSync(out, { recursive: true });
    const browser = await chromium.launch({ headless: true, executablePath: process.env.PLAYWRIGHT_BROWSER || undefined });
    try {
        const context = await browser.newContext({ viewport: { width: 1440, height: 1000 } });
        await context.grantPermissions(['clipboard-read', 'clipboard-write']);
        const page = await context.newPage();
        const errors = [];
        page.on('pageerror', error => errors.push(error.message));

        for (const code of [400, 401, 403, 404, 429, 500, 502, 503, 504, 418]) {
            const response = await page.goto(`${base}/fixture/status/${code}`);
            assert.equal(response.status(), code);
            assert.match(response.headers()['cache-control'], /no-store/);
            assert.equal(await page.locator('h1').count(), 1);
            assert.equal(await page.locator('.portal-error__eyebrow').textContent(), `Error ${code}`);
            assert.ok((await page.locator('#support-reference').innerText()).length > 0);
            assert.equal(await page.locator('.sidebar, .logout-form').count(), 0);
            assert.equal(await page.locator('.portal-error__help-link').getAttribute('href'), '/Public/Help');
            const primary = page.locator('.portal-error__button--primary');
            assert.equal(await primary.getAttribute('href'), code === 401 ? '/Home/Login' : '/');
            if (code === 401) assert.match(await primary.innerText(), /Sign in/);
        }

        const missing = await page.goto(`${base}/this-page-does-not-exist`);
        assert.equal(missing.status(), 404);
        assert.equal(await page.locator('h1').innerText(), 'Page not found');
        await page.getByRole('button', { name: 'Copy reference' }).click();
        await page.waitForFunction(() => document.querySelector('[role=status]').textContent === 'Support reference copied.');
        assert.equal(await page.locator('[role=status]').innerText(), 'Support reference copied.');
        assert.equal(await page.evaluate(() => navigator.clipboard.readText()), await page.locator('#support-reference').innerText());

        await page.evaluate(() => Object.defineProperty(navigator, 'clipboard', {
            configurable: true, value: { writeText: async () => { throw new Error('Clipboard unavailable'); } }
        }));
        await page.getByRole('button', { name: 'Copy reference' }).click();
        await page.waitForFunction(() => document.querySelector('[role=status]').textContent.includes('highlighted reference'));
        assert.match(await page.locator('[role=status]').innerText(), /highlighted reference/);
        assert.equal(await page.evaluate(() => getSelection().toString()), await page.locator('#support-reference').innerText());

        const exception = await page.goto(`${base}/fixture/throw?code=404`);
        assert.equal(exception.status(), 500, 'An original query must not disguise an exception as a 404.');
        assert.equal(await page.locator('h1').innerText(), 'Something went wrong');
        assert.ok(!(await page.locator('body').innerText()).includes('Private exception detail'));

        for (const code of ['200', '-1', '999', 'invalid', '']) {
            const response = await page.goto(`${base}/Home/Error?code=${code}`);
            assert.equal(response.status(), 500, `Invalid code ${code} must produce a safe server error.`);
        }

        const post = await context.request.post(`${base}/fixture/status/404`);
        assert.equal(post.status(), 404);
        assert.match(await post.text(), /Page not found/);

        for (const viewport of [{ width: 1440, height: 1000 }, { width: 768, height: 1024 }, { width: 390, height: 844 }, { width: 320, height: 640 }]) {
            await page.setViewportSize(viewport);
            for (const code of [401, 404, 503]) {
                await page.goto(`${base}/Home/Error?code=${code}`);
                assert.ok(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth), `Overflow at ${viewport.width}px for ${code}`);
                const button = await page.locator('.portal-error__button--primary').boundingBox();
                assert.ok(button.width >= 44 && button.height >= 44);
                if (viewport.width === 1440 || viewport.width === 390) {
                    await page.screenshot({ path: path.join(out, `${code}-${viewport.width}.png`), fullPage: true });
                }
            }
        }

        await page.setViewportSize({ width: 1440, height: 1000 });
        await page.goto(`${base}/Home/Error?code=404`);
        await page.keyboard.press('Tab');
        assert.equal(await page.evaluate(() => document.activeElement.textContent), 'Skip to main content');
        await page.keyboard.press('Enter');
        assert.ok(page.url().endsWith('#main-content'));
        assert.equal((await context.request.get(`${base}/css/error-page.css`)).status(), 200);
        assert.equal((await context.request.get(`${base}/js/error-page.js`)).status(), 200);
        assert.deepEqual(errors, []);

        const noScript = await browser.newContext({ javaScriptEnabled: false, viewport: { width: 390, height: 844 } });
        const noScriptPage = await noScript.newPage();
        await noScriptPage.goto(`${base}/Home/Error?code=404`);
        assert.equal(await noScriptPage.locator('[data-copy-reference]').isVisible(), false);
        assert.equal(await noScriptPage.locator('.portal-error__button--primary').isVisible(), true);
        assert.equal(await noScriptPage.locator('#support-reference').isVisible(), true);
        console.log('PASS: status/exception handling, invalid codes, POST errors, copy/fallback, navigation, desktop/tablet/mobile layouts, keyboard access, and JavaScript-disabled rendering.');
    } finally {
        await browser.close();
    }
})().catch(error => { console.error(error); process.exitCode = 1; });
