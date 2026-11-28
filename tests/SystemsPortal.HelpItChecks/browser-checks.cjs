const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { chromium } = require(process.env.PLAYWRIGHT_MODULE || 'playwright');
const base = 'http://127.0.0.1:5194';
const output = path.resolve('artifacts/help-it-domain-login');

(async () => {
    fs.mkdirSync(output, { recursive: true });
    const browser = await chromium.launch({ headless: true, executablePath: process.env.PLAYWRIGHT_BROWSER || undefined });
    try {
        const context = await browser.newContext({ viewport: { width: 1440, height: 1100 } });
        const page = await context.newPage();
        await page.request.post(base + '/fixture/reset');
        const errors = [];
        page.on('pageerror', e => errors.push(e.message));
        await page.goto(base + '/Public/Help');
        const helpPageUrl = page.url();
        const loginDialog = page.getByRole('dialog', { name: 'Log in to submit a ticket' });
        assert.equal(await loginDialog.count(), 0);
        await page.getByRole('link', { name: 'Submit an IT ticket' }).click();
        await loginDialog.waitFor({ state: 'visible' });
        assert.equal(page.url(), helpPageUrl);
        assert.match(await page.locator('h1').innerText(), /Less downtime/);
        const domainLogin = page.getByRole('link', { name: 'Log in with your domain account' });
        assert.match(await domainLogin.getAttribute('href'), /\/Home\/WindowsLogin\?returnUrl=/);
        assert.equal(await page.locator('#helpItTicketForm').count(), 0);
        const checkDialogCentered = async () => {
            const bounds = await loginDialog.boundingBox();
            const viewport = page.viewportSize();
            assert.ok(Math.abs(bounds.x + bounds.width / 2 - viewport.width / 2) < 2);
            assert.ok(Math.abs(bounds.y + bounds.height / 2 - viewport.height / 2) < 2);
        };
        await checkDialogCentered();
        await page.screenshot({ path: path.join(output, 'desktop-login.png'), fullPage: true });
        await page.setViewportSize({ width: 390, height: 844 });
        await checkDialogCentered();
        assert.equal(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth), true);
        await page.screenshot({ path: path.join(output, 'mobile-login.png'), fullPage: true });
        await page.setViewportSize({ width: 1440, height: 1100 });
        await page.keyboard.press('Escape');
        assert.equal(await page.locator('#ticketLoginDialog').isVisible(), false);
        await page.getByRole('link', { name: 'Submit an IT ticket' }).click();
        await page.getByRole('button', { name: 'Close login dialog' }).click();
        assert.equal(page.url(), helpPageUrl);
        assert.equal(await page.locator('#ticketLoginDialog').isVisible(), false);
        await page.goto(base + '/HelpIt');
        await loginDialog.waitFor({ state: 'visible' });
        assert.match(page.url(), /\/Public\/Help$/);
        const checkBlocked = async () => {
            const csrf = (await (await page.request.get(base + '/fixture/antiforgery')).json()).token;
            for (const action of ['Create', 'ResolveContactEmail']) {
                const blocked = await page.request.post(base + '/HelpIt/' + action, {
                    form: { __RequestVerificationToken: csrf, ConfirmedSubmission: 'true', Issue: 'Must not be saved' },
                    headers: { 'X-Requested-With': 'XMLHttpRequest' }
                });
                assert.equal(blocked.status(), 401);
                assert.equal((await blocked.json()).loginUrl, '/HelpIt/Login');
            }
            assert.deepEqual(await (await page.request.get(base + '/fixture/state')).json(), { created: 0, notifications: 0 });
        };
        await checkBlocked();
        const blockedFormToken = (await (await page.request.get(base + '/fixture/antiforgery')).json()).token;
        const blockedForm = await page.request.post(base + '/HelpIt/Create', {
            form: { __RequestVerificationToken: blockedFormToken }, maxRedirects: 0
        });
        assert.equal(blockedForm.status(), 302);
        assert.equal(blockedForm.headers().location, '/HelpIt/Login');
        await page.goto(base + '/HelpIt/Tracker');
        assert.equal(await page.locator('h1').count(), 1);
        await page.request.post(base + '/fixture/login/portal');
        await page.goto(base + '/HelpIt');
        await loginDialog.waitFor({ state: 'visible' });
        assert.match(page.url(), /\/Public\/Help$/);
        await checkBlocked();
        await page.request.post(base + '/fixture/login/domain');
        await page.goto(base + '/HelpIt/Login');
        assert.doesNotMatch(page.url(), /\/Login$/);
        assert.equal(await page.locator('#classification').isDisabled(), true);
        assert.equal(await page.locator('#callerNameInput').count(), 0);
        assert.match(await page.locator('.help-it-caller-summary').innerText(), /Example Domain Employee/);
        await page.locator('#companyId').selectOption('1');
        await page.locator('#departmentId').selectOption('2');
        await page.locator('#branchId').selectOption('3');
        await page.locator('#issueType').selectOption('Hardware');
        await page.locator('#classification').selectOption('Computer');
        assert.equal(await page.locator('#deviceName').getAttribute('required'), '');
        await page.locator('#deviceName').fill('YPI-TEST-PC001');
        await page.locator('#issue').fill('Computer will not start.\nPower light turns on, but the screen stays blank.');
        await page.evaluate(() => window.scrollTo(0, 0));
        await page.screenshot({ path: path.join(output, 'desktop-form.png'), fullPage: true });
        await page.getByRole('button', { name: 'Review ticket' }).click();
        assert.equal(await page.locator('#helpItReviewClassification').innerText(), 'Computer');
        assert.equal(await page.locator('#helpItReviewDeviceName').innerText(), 'YPI-TEST-PC001');
        assert.equal((await (await page.request.get(base + '/fixture/state')).json()).created, 0);
        await page.locator('#helpItContactEmail').fill('requester@example.invalid');
        await page.screenshot({ path: path.join(output, 'desktop-review.png') });
        await page.locator('#helpItReviewConfirm').click();
        await page.locator('#helpItSuccessPanel').waitFor({ state: 'visible' });
        assert.equal(await page.locator('#helpItSuccessCode').innerText(), 'TCK-000001');
        assert.match(await page.locator('#helpItSuccessPanel').innerText(), /Example Domain Employee/);
        assert.match(await page.locator('#helpItNotificationMessage').innerText(), /notified/);
        await page.screenshot({ path: path.join(output, 'desktop-success.png') });
        await page.locator('#helpItSuccessClose').click();
        await page.getByRole('button', { name: 'Review ticket' }).click();
        assert.equal(await page.locator('#helpItSuccessPanel').isVisible(), true);
        assert.deepEqual(await (await page.request.get(base + '/fixture/state')).json(), { created: 1, notifications: 1 });
        await page.locator('#helpItSuccessDetails').click();
        assert.match(await page.locator('.ticket-facts--request').innerText(), /Computer/);
        assert.match(await page.locator('.ticket-facts--request').innerText(), /YPI-TEST-PC001/);

        await page.goto(base + '/HelpIt');
        const token = await page.locator('#helpItTicketForm input[name="__RequestVerificationToken"]').inputValue();
        const form = { __RequestVerificationToken: token, ConfirmedSubmission: 'true', ConfirmedRepeatSubmission: 'true',
            CompanyId: '1', DepartmentId: '2', CallerNameInput: 'Example Employee', IssueType: 'Hardware',
            Classification: 'Computer', DeviceName: 'TEST-PC', Issue: 'Does not start', ContactEmail: 'requester@example.invalid' };
        const submit = data => page.request.post(base + '/HelpIt/Create', { form: { ...form, ...data }, headers: { 'X-Requested-With': 'XMLHttpRequest' } });
        assert.equal((await submit({ ConfirmedSubmission: 'false' })).status(), 400);
        assert.equal((await submit({ ConfirmedRepeatSubmission: 'false' })).status(), 400);
        assert.equal((await submit({ Classification: 'VPN' })).status(), 400);
        assert.equal((await submit({ DeviceName: '' })).status(), 400);
        assert.equal((await submit({ Issue: 'x'.repeat(2001) })).status(), 400);
        assert.equal((await submit({ Issue: 'x'.repeat(1990) })).status(), 400); // Metadata also uses the existing description capacity.
        assert.equal((await submit({ DeviceName: 'x'.repeat(151) })).status(), 400);
        assert.equal((await submit({ ContactEmail: 'invalid' })).status(), 400);
        assert.equal((await submit({ CompanyId: '999' })).status(), 400);
        await page.request.post(base + '/fixture/ready/false');
        assert.equal((await submit({})).status(), 503);
        assert.equal((await (await page.request.get(base + '/fixture/state')).json()).created, 1);
        await page.request.post(base + '/fixture/ready/true');
        await page.request.post(base + '/fixture/mail/true');
        const response = await submit({ IssueType: 'Software', Classification: 'Business application', DeviceName: '' });
        assert.equal(response.status(), 200);
        const ticket = await response.json();
        assert.equal(ticket.notificationSent, false);
        assert.match(ticket.notificationMessage, /saved/);
        assert.equal(ticket.ticketCode, 'TCK-000002');
        assert.deepEqual(await (await page.request.get(base + '/fixture/state')).json(), { created: 2, notifications: 2 });

        const mobile = await context.newPage();
        await mobile.setViewportSize({ width: 390, height: 844 });
        await mobile.goto(base + '/HelpIt');
        await mobile.locator('#issueType').selectOption('Hardware');
        await mobile.locator('#classification').selectOption('Computer');
        await mobile.locator('#issueType').selectOption('Network');
        assert.equal(await mobile.locator('#classification').inputValue(), '');
        assert.equal(await mobile.locator('#deviceName').getAttribute('required'), null);
        assert.equal(await mobile.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth), true);
        await mobile.screenshot({ path: path.join(output, 'mobile-form.png'), fullPage: true });
        await mobile.locator('#companyId').selectOption('1');
        await mobile.locator('#departmentId').selectOption('2');
        await mobile.locator('#classification').selectOption('Internet');
        await mobile.locator('#issue').fill('Internet connection drops during work.');
        await mobile.getByRole('button', { name: 'Review ticket' }).click();
        await mobile.locator('#helpItReviewPanel').waitFor({ state: 'visible' });
        assert.equal(await mobile.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth), true);
        await mobile.screenshot({ path: path.join(output, 'mobile-review.png') });
        await mobile.keyboard.press('Escape');
        assert.equal(await mobile.locator('#helpItReviewDialog').isHidden(), true);
        assert.deepEqual(errors, []);
        await page.request.post(base + '/fixture/login/linked');
        await page.goto(base + '/HelpIt');
        assert.equal(await page.locator('#helpItTicketForm').count(), 1);
        assert.equal(await page.locator('#companyId').count(), 0);
        await page.request.post(base + '/fixture/logout');
        await page.goto(base + '/HelpIt');
        await loginDialog.waitFor({ state: 'visible' });
        assert.match(page.url(), /\/Public\/Help$/);
        console.log('PASS: ticket links open a dismissible domain-login dialog without leaving the existing UI; direct access and submissions remain gated; domain access, desktop/mobile submission, validation, and email failure handling pass.');
    } finally { await browser.close(); }
})().catch(e => { console.error(e); process.exitCode = 1; });
