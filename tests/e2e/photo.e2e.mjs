// End-to-end runs of the Kazo Photo window inside the Kazo OS host (doc/en/plan.md, work item 1):
// `npm run test:e2e` after `dotnet build Kazo.Photo.slnx -c Debug`. Each test opens a fresh page.
//
// The window shows PhotoCraft once PhotoCraft says "ready" (photocraft:ready), which only the host
// mode of our fork sends (fork work item F2).
// The PhotoCraft build is 0.23.1 as released (kazo/v0.23.1) until then. Until the submodule moves to a build with it, the
// test that waits for "ready" is marked todo: it runs and reports, without failing the suite.
import { after, before, test } from 'node:test';
import assert from 'node:assert/strict';
import { openApp, runInTerminal, startHost } from './host.mjs';
import { bootPage, frameAt, launchBrowser, photoWindows, rendererOf, waitForPhotoCraft } from './common.mjs';

let host;
let browser;

before(async () => {
    host = await startHost();
    browser = await launchBrowser();
});

after(async () => {
    await browser?.close();
    host?.stop();
});

test('Kazo Photo opens from the Applications menu and PhotoCraft starts in its iframe', { timeout: 300_000 }, async () => {
    const { page, errors } = await bootPage(browser, host.baseUrl);

    await openApp(page, 'Kazo Photo');
    const frame = await frameAt(page, 0);
    const query = new URL(frame.url()).searchParams;
    assert.equal(query.has('control'), true, 'PhotoCraft installs window.photocraft only with ?control');
    assert.equal(query.has('webgl'), false);

    // PhotoCraft picks WebGPU when the browser has it. Chromium's software WebGPU adapter
    // (SwiftShader, headless) loses its device right after the start ("A valid external Instance
    // reference no longer exists"), so drawing on WebGPU is checked on a real GPU, not here; the
    // --webgl run below draws.
    await waitForPhotoCraft(frame);
    assert.equal(await frame.locator('#photocraft_stopped').isHidden(), true, 'PhotoCraft did not stop');

    assert.deepEqual(errors, []);
    await page.close();
});

test('the --webgl launch argument starts PhotoCraft on WebGL 2', { timeout: 300_000 }, async () => {
    const { page, errors } = await bootPage(browser, host.baseUrl);

    await runInTerminal(page, 'launch ca.kazo.photo --webgl');
    const frame = await frameAt(page, 0);
    assert.equal(new URL(frame.url()).searchParams.has('webgl'), true);
    await waitForPhotoCraft(frame);
    assert.equal(await rendererOf(frame), 'webgl2');

    assert.deepEqual(errors, []);
    await page.close();
});

test('two Kazo Photo windows run their own PhotoCraft at the same time', { timeout: 400_000 }, async () => {
    const { page, errors } = await bootPage(browser, host.baseUrl);

    await openApp(page, 'Kazo Photo');
    const first = await frameAt(page, 0);
    await waitForPhotoCraft(first);
    await openApp(page, 'Kazo Photo');
    const second = await frameAt(page, 1);
    await waitForPhotoCraft(second);

    assert.equal(await photoWindows(page).count(), 2);
    assert.notEqual(first, second);
    assert.equal(await first.locator('#photocraft_stopped').isHidden(), true);
    assert.equal(await second.locator('#photocraft_stopped').isHidden(), true);

    assert.deepEqual(errors, []);
    await page.close();
});

test('the window shows PhotoCraft once it is ready', { timeout: 300_000, todo: 'needs the host mode of the fork (F2): photocraft:ready' }, async () => {
    const { page, errors } = await bootPage(browser, host.baseUrl);

    await openApp(page, 'Kazo Photo');
    await page.locator('.photo-frame.running').first().waitFor({ timeout: 120_000 });
    assert.equal(await page.locator('.photo-screen').count(), 0, 'no loading or error screen is left');

    assert.deepEqual(errors, []);
    await page.close();
});
