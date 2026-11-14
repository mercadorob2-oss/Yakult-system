const assert = require('node:assert/strict');
const path = require('node:path');
const os = require('node:os');
const { chromium } = require(path.join(os.tmpdir(), 'yakult-learning-qa/node_modules/playwright'));

(async () => {
    const browser = await chromium.launch({ headless: true, executablePath: path.join(process.env.LOCALAPPDATA, 'ms-playwright/chromium-1228/chrome-win64/chrome.exe') });
    try {
        const page = await browser.newPage();
        await page.goto('http://127.0.0.1:5188/company-policies/it-policy---employee-guidelines');
        const steps = page.locator('#guest-internet .it-info-procedure > ol > li');
        assert.equal(await steps.count(), 7);
        for (let i = 0; i < 7; i++) {
            assert.equal(await steps.nth(i).getAttribute('value'), String(i + 1));
            assert.equal(await steps.nth(i).evaluate(el => getComputedStyle(el, '::before').content), '"' + (i + 1) + '"');
        }
        for (const number of [3, 4]) {
            assert.equal(await steps.nth(number - 1).locator('img').count(), 1);
            assert.match(await steps.nth(number - 1).locator('img').getAttribute('src'), new RegExp('guest-registration-word-step-' + number + '-'));
        }
        assert.match(await steps.nth(4).locator('img').getAttribute('src'), /guest-wifi-step-6-/);
        assert.match(await steps.nth(5).locator('img').getAttribute('src'), /guest-wifi-step-5-/);
        assert.equal(await page.locator('#guest-internet img').count(), 7);
        assert.match(await steps.nth(6).locator('img').getAttribute('src'), /guest-wifi-step-7-/);
        console.log('Guest procedure checks passed: sequential labels, registration images 3/4 restored, images 5/6 swapped and paired.');
    } finally { await browser.close(); }
})().catch(error => { console.error(error); process.exitCode = 1; });
