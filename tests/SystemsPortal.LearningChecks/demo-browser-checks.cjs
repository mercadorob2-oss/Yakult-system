const assert = require('node:assert/strict');
const path = require('node:path');
const os = require('node:os');
const fs = require('node:fs');
const { chromium } = require(path.join(os.tmpdir(), 'yakult-learning-qa', 'node_modules', 'playwright'));

(async () => {
    const browser = await chromium.launch({ headless: true, executablePath: path.join(process.env.LOCALAPPDATA, 'ms-playwright', 'chromium-1228', 'chrome-win64', 'chrome.exe') });
    try {
        const context = await browser.newContext({ viewport: { width: 1440, height: 1000 } });
        await context.route('**/*', route => new URL(route.request().url()).hostname === '127.0.0.1' ? route.continue() : route.abort());
        const page = await context.newPage();
        const errors = [];
        page.on('pageerror', error => errors.push(error.message));
        page.on('dialog', dialog => dialog.accept());
        page.setDefaultTimeout(10000);
        const base = 'http://127.0.0.1:5187';
        const screenshots = path.join(os.tmpdir(), 'yakult-learning-qa', 'screenshots');
        fs.mkdirSync(screenshots, { recursive: true });
        await context.addCookies([{ name: 'learning-fixture-demo', value: 'true', url: base }, { name: 'learning-fixture-anonymous', value: 'true', url: base }]);
        await page.goto(base + '/Public/Learning');
        assert.equal(await page.locator('.it-info-resource').count(), 2);
        assert.ok(await page.getByRole('heading', { name: 'Your guide to IT at Yakult.' }).isVisible());
        assert.equal(await page.locator('.academy-progress, [data-course-key], [download]').count(), 0);
        await page.screenshot({ path: path.join(screenshots, 'it-information-desktop.png'), fullPage: true });
        await page.getByRole('searchbox', { name: 'Search these resources' }).fill('Comptroller');
        await Promise.all([page.waitForURL(/q=Comptroller/), page.getByRole('button', { name: 'Search IT resources' }).click()]);
        assert.equal(await page.locator('.it-info-resource').count(), 1);
        assert.match(await page.locator('.it-info-resource h3').innerText(), /IT Policy/);
        await page.goto(base + '/Public/Learning?q=unmatched-phrase');
        assert.ok(await page.getByRole('heading', { name: 'No matching resources' }).isVisible());
        await page.goto(base + '/Public/Learning?q=FortiGate');
        assert.equal(await page.locator('.it-info-resource').count(), 0, 'Operational configuration cannot be searched');
        await page.goto(base + '/Public/Search?q=Comptroller');
        assert.ok(await page.getByRole('link', { name: /IT Policy & Employee Guidelines/ }).count() > 0);
        const policyUrl = base + '/content/demo-it-information-policy-and-guidelines';
        const teamUrl = base + '/content/demo-it-information-meet-it-and-business-systems';
        for (const [url, topicCount] of [[policyUrl, 4], [teamUrl, 3]]) {
            const response = await page.goto(url);
            assert.equal(response.status(), 200, 'Anonymous resource opens directly');
            assert.equal(await page.locator('.it-info-section').count(), topicCount);
            assert.equal(await page.locator('.it-info-source').count(), topicCount);
            assert.equal(await page.locator('video, [data-complete], [data-course-key], a[download]').count(), 0);
            const html = await page.content();
            for (const excluded of ['FortiGate', 'Network Layout', 'Contract Renewal', '[Details]', 'Internet Explorer', 'Business Central']) assert.ok(!html.includes(excluded));
            assert.equal(await page.locator('a[href$=".docx"], a[href$=".pptx"]').count(), 0);
            if (url === policyUrl) {
                assert.deepEqual(await page.locator('.it-info-section > h2').allTextContents(), ['Workplace Guidelines', 'Profile Account', 'Internet Access', 'Flowcharts']);
                assert.equal(await page.locator('.it-info-flowchart').count(), 3);
                for (const chart of await page.locator('.it-info-flowchart').all()) {
                    if (!(await chart.evaluate(element => element.open))) await chart.locator(':scope > summary').click();
                    assert.ok(await chart.locator('figure').isVisible());
                    assert.ok(await chart.locator('figcaption').isVisible());
                }
                assert.equal(await page.locator('.it-info-source-diagram').count(), 3);
                for (const diagram of await page.locator('.it-info-source-diagram').all()) {
                    await diagram.scrollIntoViewIfNeeded();
                    await diagram.evaluate(image => image.decode());
                    assert.ok(await diagram.evaluate(image => image.naturalWidth > 0));
                }
                assert.equal(await page.locator('.it-info-account-process > ol > li').count(), 3);
                assert.match(await page.locator('.it-info-account-process').innerText(), /3–6 months/);
                assert.match(await page.locator('.it-info-source-note').innerText(), /one month duration to delete/);
                await page.locator('.it-info-source-section summary').click();
                await page.locator('.it-info-source-section img').scrollIntoViewIfNeeded();
                await page.locator('.it-info-source-section img').evaluate(image => image.decode());
                assert.ok(await page.locator('.it-info-source-section img').evaluate(image => image.naturalWidth > 0));
                const assetResponse = await context.request.get(base + '/media/learning/demo/policy/hardware-deployment.svg');
                assert.equal(assetResponse.status(), 200);
                assert.equal(assetResponse.headers()['cache-control'], 'no-store');
                assert.equal((await context.request.get(base + '/App_Data/PolicyPreview/hardware-deployment.svg')).status(), 404, 'Source assets are outside static files');
            }
            await page.screenshot({ path: path.join(screenshots, url === policyUrl ? 'it-policy-desktop.png' : 'it-team-desktop.png'), fullPage: true });
            for (const width of [390, 320]) {
                await page.setViewportSize({ width, height: 844 });
                assert.ok(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth), 'Reader has no overflow at ' + width);
                if (url === policyUrl) {
                    for (const chart of await page.locator('.it-info-flowchart').all()) {
                        const bounds = await chart.boundingBox();
                        assert.ok(bounds.x >= 0 && bounds.x + bounds.width <= width, 'Flowchart fits at ' + width);
                    }
                }
                await page.locator('.it-info-outline summary').click();
                assert.equal(await page.locator('.it-info-outline details').evaluate(element => element.open), false);
                await page.locator('.it-info-outline summary').click();
                await page.locator('.it-info-outline nav a').last().click();
                assert.ok(new URL(page.url()).hash.length > 1);
                await page.screenshot({ path: path.join(screenshots, `it-${url === policyUrl ? 'policy' : 'team'}-${width}.png`), fullPage: true });
            }
            await page.setViewportSize({ width: 1440, height: 1000 });
        }
        assert.equal(await page.locator('.it-info-detail-card').count(), 9, 'Six IT functions and three system cards');
        for (const width of [390, 320]) {
            await page.setViewportSize({ width, height: 844 });
            await page.goto(base + '/Public/Learning');
            assert.ok(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth), 'Landing has no overflow at ' + width);
            await page.screenshot({ path: path.join(screenshots, `it-information-${width}.png`), fullPage: true });
        }
        await context.addCookies([{ name: 'learning-fixture-anonymous', value: 'false', url: base }]);
        await page.setViewportSize({ width: 1440, height: 1000 });
        // Existing authoring/course fixtures remain a separate compatibility check.
        const sampleUrl = base + '/content/demo-learning-cybersecurity-essentials';
        await page.goto(sampleUrl);
        await page.waitForFunction(() => document.querySelector('.academy-block video').duration >= 3.9);
        const videoResponse = await context.request.get(base + '/media/learning/demo/lesson-preview.webm', { headers: { Range: 'bytes=0-1023' } });
        assert.equal(videoResponse.status(), 206, 'Local demo video supports range playback');
        const download = await context.request.get(base + '/media/learning/demo/learning-checklist.txt');
        assert.match(await download.text(), /DEMO CHECKLIST/);
        await page.locator('#academyStart').click();
        await page.locator('[data-lesson]:visible [data-complete]').click();
        await page.locator('[data-lesson]:visible img').waitFor();
        assert.ok(await page.locator('[data-lesson]:visible img').evaluate(image => image.naturalWidth > 0));
        await page.setViewportSize({ width: 390, height: 844 });
        assert.ok(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth));
        await page.screenshot({ path: path.join(os.tmpdir(), 'yakult-learning-qa', 'screenshots', 'demo-course-mobile.png'), fullPage: true });
        await page.goto(base + '/Public/MyLearning');
        assert.ok(await page.locator('#academyMyLearning').isVisible());
        assert.equal(await page.locator('.academy-browser-course').count(), 1);
        await page.goto(base + '/Admin/LearningContent');
        assert.equal(await page.locator('.studio-item').count(), 15);
        assert.ok(await page.locator('#courseBuilder').isVisible());
        assert.ok(await page.locator('#saveDraftBtn').isDisabled());
        assert.equal(await page.locator('#publishContent').count(), 0);
        assert.ok(await page.locator('.builder-block-upload input').isDisabled());
        assert.ok(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth));
        await page.locator('#builderLessonTitle').fill('Temporary demo edit');
        await page.reload();
        assert.equal(await page.locator('#builderLessonTitle').inputValue(), 'Start with the essentials');
        const save = await context.request.post(base + '/Admin/LearningContent/Save', { data: { contentId: -3001 } });
        assert.equal(save.status(), 400);
        await context.clearCookies();
        for (const url of [policyUrl, teamUrl]) assert.equal((await context.request.get(url)).status(), 404, 'Reference preview requires demo mode');
        assert.equal((await context.request.get(base + '/media/learning/demo/policy/profile-account.svg')).status(), 404, 'Source section requires demo mode');
        await page.goto(base + '/Public/MyLearning');
        assert.ok(await page.locator('#academyMyLearning').isHidden(), 'Demo progress is absent in live view');
        const off = await context.request.get(sampleUrl);
        assert.equal(off.status(), 404, 'Sample course requires demo mode');
        const offAsset = await context.request.get(base + '/media/learning/demo/email-example.svg');
        assert.equal(offAsset.status(), 404, 'Demo media route requires demo mode');
        assert.deepEqual(errors, []);
        console.log('PASS: two public IT references, anonymous reading, content exclusions, search, mobile navigation at 390/320px, mode isolation, and existing course/editor compatibility.');
    } finally { await browser.close(); }
})().catch(error => { console.error(error); process.exitCode = 1; });
