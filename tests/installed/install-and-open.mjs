// Kazo Photo as the Kazo OS Store installs it (work item 3 of doc/en/plan.md):
// `npm run test:installed -- [--store] <Kazo OS wwwroot> [packages] [screenshots folder]`.
//
// --store: installs from the real Kazo OS Store (the published catalog of Os-AppMarketPlace, read
//   over the network) instead of `kazopkg install` from [packages]: the check after a release.
//
// <Kazo OS wwwroot>: a published Kazo OS, for example the kazoos-wasm-<version>.zip of a Kazo OS
//   release, unzipped (the trimmed OS users run; 3.3.4 is the minimum).
// [packages]: the folder `dotnet build src/Kazo.Photo -c Release -t:KazoOsPack` writes (default
//   src/Kazo.Photo/bin/kazoos-packages), the content of the kazoos-packages branch.
// [screenshots folder]: when given, the run also writes the Store's screenshots there (1.png …).
//
// The run serves the OS as a plain static host, answers the GitHub addresses of this repository's
// packages from [packages], installs Kazo Photo with `sudo kazopkg install` (its API check against
// the trimmed runtime included), uploads a PNG drawn by sample-image.mjs into the home folder,
// starts Kazo Photo from the launcher (no launch argument: PhotoCraft picks its renderer itself),
// opens the PNG through PhotoCraft's File › Open and the OS dialog, adds an adjustment layer and
// saves: the image now has layers, so the OS save dialog writes landscape.psd. Then a double-click
// on landscape.psd in the File Explorer opens it, a layer is added and saved in place, and the
// licenses are checked. It prints how long each window took to show PhotoCraft. Not part of CI: it
// needs WebGL 2 or WebGPU in the browser and several minutes.
import { createServer } from 'node:http';
import { mkdir, mkdtemp, readFile, stat, writeFile } from 'node:fs/promises';
import os from 'node:os';
import { fileURLToPath } from 'node:url';
import path from 'node:path';
import { chromium } from 'playwright';
import { landscapePng } from './sample-image.mjs';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', '..');
const fromStore = process.argv.includes('--store');
const [wwwrootArgument, packagesArgument, shotsArgument] = process.argv.slice(2).filter(argument => argument !== '--store');
if (!wwwrootArgument) {
    console.error('usage: npm run test:installed -- <Kazo OS wwwroot> [packages folder] [screenshots folder]');
    process.exit(2);
}
const wwwroot = path.resolve(wwwrootArgument);
const packages = path.resolve(packagesArgument ?? path.join(root, 'src', 'Kazo.Photo', 'bin', 'kazoos-packages'));
const shots = shotsArgument && path.resolve(shotsArgument);

const REPOSITORY = 'Kazo-ca/Os-App-Photo';
const APP = 'Kazo Photo';
const LOGIN = 'neo';
const PASSWORD = 'matrix123';
const HOME = `/home/${LOGIN}`;
const STEP_MS = 120_000;
// PhotoCraft's first start downloads about 40 MB of .wasm and compiles it.
const START_MS = 240_000;
const SAVE_TITLE = 'Save the image';
const TYPES = {
    '.html': 'text/html', '.js': 'text/javascript', '.mjs': 'text/javascript', '.css': 'text/css',
    '.json': 'application/json', '.wasm': 'application/wasm', '.svg': 'image/svg+xml', '.png': 'image/png',
    '.webmanifest': 'application/manifest+json', '.dat': 'application/octet-stream', '.blat': 'application/octet-stream',
};
// No WebGPU flag: Chromium on Linux has no WebGPU by default, so PhotoCraft falls back to WebGL 2
// by itself, which ANGLE draws on the CPU (SwiftShader). Chromium's software WebGPU adapter loses
// its device in headless runs (tests/e2e/photo.e2e.mjs), so it is not used here.
const browserOptions = { args: ['--use-angle=swiftshader', '--enable-unsafe-swiftshader', '--ignore-gpu-blocklist'] };

const log = message => console.log(`[installed] ${message}`);

// A plain static server, as a release host is: no COOP/COEP, no cache.
const server = createServer(async (request, response) => {
    const relative = decodeURIComponent(new URL(request.url, 'http://host').pathname);
    let file = path.join(wwwroot, relative);
    if (!file.startsWith(wwwroot)) {
        response.writeHead(403).end();
        return;
    }
    try {
        if ((await stat(file)).isDirectory()) file = path.join(file, 'index.html');
        const body = await readFile(file);
        response.writeHead(200, { 'Content-Type': TYPES[path.extname(file)] ?? 'application/octet-stream', 'Cache-Control': 'no-store' });
        response.end(body);
    } catch {
        response.writeHead(404).end();
    }
});
await new Promise(resolve => server.listen(0, '127.0.0.1', resolve));
const baseUrl = `http://127.0.0.1:${server.address().port}/`;

const waitForText = async (locator, predicate, what) => {
    const deadline = Date.now() + STEP_MS;
    while (Date.now() < deadline) {
        const text = (await locator.innerText()).replace(/ /g, ' ');
        if (predicate(text)) return text;
        await new Promise(resolve => setTimeout(resolve, 250));
    }
    throw new Error(`timed out waiting for ${what}`);
};

const photoWindows = page => page.locator('article[data-window-id]', { has: page.locator('.photo-window') });
const windowById = (page, id) => page.locator(`article[data-window-id="${id}"]`);

/** Waits for a new Kazo Photo window showing PhotoCraft; returns its id and how long it took. */
async function waitForNewWindow(page, known, timeout = START_MS) {
    const started = Date.now();
    const deadline = started + timeout;
    while (Date.now() < deadline) {
        const ids = await photoWindows(page).evaluateAll(nodes => nodes
            .filter(node => node.querySelector('.photo-frame.running'))
            .map(node => node.getAttribute('data-window-id')));
        const id = ids.find(candidate => !known.includes(candidate));
        if (id) return { id, ms: Date.now() - started };
        await new Promise(resolve => setTimeout(resolve, 100));
    }
    const failure = await photoWindows(page).locator('.photo-screen.failed').allInnerTexts().catch(() => []);
    throw new Error(`no new Kazo Photo window showed PhotoCraft ${failure.join(' | ')}`);
}

async function frameOf(page, id) {
    const handle = await windowById(page, id).locator('iframe.craft-frame-iframe').elementHandle();
    return handle.contentFrame();
}

/** A PhotoCraft method (window.photocraft, the page API of ?control). */
const request = (frame, method, params = {}) => frame.evaluate(([name, args]) => window.photocraft.request(name, args), [method, params]);
/** A PhotoCraft engine command, as its menus run it. */
const execute = (frame, command, params = {}) => request(frame, 'engine.execute', { command, params });
/** A PhotoCraft menu item (File › Save…), which opens what the menu opens. */
const menu = (frame, id) => request(frame, 'ui.menu.invoke', { id });

/** Waits until one of PhotoCraft's open documents (session.inspect) matches `predicate`. */
async function waitForDocument(frame, predicate, what) {
    const deadline = Date.now() + STEP_MS;
    let last = [];
    while (Date.now() < deadline) {
        last = await execute(frame, 'session.inspect').then(answer => answer?.documents ?? answer?.result?.documents ?? []).catch(error => [String(error)]);
        if (last.some(doc => typeof doc === 'object' && predicate(doc))) return;
        await new Promise(resolve => setTimeout(resolve, 250));
    }
    throw new Error(`no document ${what}: ${JSON.stringify(last)}`);
}

/** Answers the OS save dialog titled `title` with a file name. */
async function answerSaveDialog(page, title, fileName) {
    const dialog = page.locator('section.file-dialog', { hasText: title });
    await dialog.waitFor({ state: 'visible', timeout: STEP_MS });
    await dialog.getByLabel('File name').fill(fileName);
    await dialog.getByRole('button', { name: 'Save' }).click();
    await dialog.waitFor({ state: 'hidden', timeout: STEP_MS });
}

async function expectTitle(page, id, text) {
    await page.waitForFunction(
        ([windowId, wanted]) => document.querySelector(`article[data-window-id="${windowId}"] header.window-chrome h2`)?.textContent?.trim() === wanted,
        [id, text],
        { timeout: STEP_MS });
}

async function shoot(page, name) {
    if (!shots) return;
    await mkdir(shots, { recursive: true });
    // Let PhotoCraft paint its last frame.
    await page.waitForTimeout(1500);
    await page.screenshot({ path: path.join(shots, name) });
    log(`screenshot ${name}`);
}

async function openApp(page, name) {
    await page.getByRole('button', { name: 'App launcher' }).click({ timeout: STEP_MS });
    await page.getByRole('combobox', { name: 'Search applications' }).fill(name);
    await page.getByRole('button', { name: `Launch ${name}`, exact: true }).click();
}

/** `sudo kazopkg install` in the Terminal, as an administrator. */
async function installWithKazopkg(page) {
    await openApp(page, 'Terminal');
    const rows = page.locator('[data-terminal-panel] .xterm-rows').first();
    await waitForText(rows, text => text.includes('$'), 'the shell prompt');
    await page.locator('[data-terminal-panel] .xterm-screen').first().click();
    await page.keyboard.type(`sudo kazopkg install ${REPOSITORY}`);
    await page.keyboard.press('Enter');
    const prompt = page.locator('.credential-prompt-dialog, .aaa-window').first();
    await prompt.waitFor({ state: 'visible', timeout: STEP_MS });
    const login = prompt.locator('input[type=text]');
    if (await login.count() > 0 && !(await login.inputValue())) await login.fill(LOGIN);
    await prompt.locator('input[type=password]').fill(PASSWORD);
    await prompt.locator('input[type=password]').press('Enter');
    const output = await waitForText(rows, text => text.includes('available now') || /does not have|refused|error/i.test(text), 'the end of the installation');
    if (!output.includes('available now')) throw new Error(`kazopkg did not install ${APP}:\n${output}`);
    log(`kazopkg installed ${APP}.`);
    await page.locator('[data-terminal-panel]').first().locator('xpath=ancestor::article[1]').locator('header.window-chrome .chrome-button-close').click();
}

/** The Kazo OS Store: search Kazo Photo in the published catalog, "Install for me". */
async function installFromStore(page) {
    await openApp(page, 'Kazo OS Store');
    const store = page.locator('.store-root').first();
    await store.locator('input.store-search').fill(APP);
    // Kazo OS 3.3.4 Store: a card (not a button) whose title opens the app's page.
    const card = store.locator('.store-card', { hasText: APP }).first();
    await card.waitFor({ timeout: STEP_MS });
    await card.getByText(APP, { exact: true }).first().click();
    await store.getByRole('button', { name: 'Install for me', exact: true }).click({ timeout: STEP_MS });
    // A first install from the catalog may ask an administrator.
    const prompt = page.locator('.credential-prompt-dialog, .aaa-window').first();
    const opened = store.getByRole('button', { name: 'Open', exact: true });
    await Promise.race([
        prompt.waitFor({ state: 'visible', timeout: STEP_MS }).then(async () => {
            const login = prompt.locator('input[type=text]');
            if (await login.count() > 0 && !(await login.inputValue())) await login.fill(LOGIN);
            await prompt.locator('input[type=password]').fill(PASSWORD);
            await prompt.locator('input[type=password]').press('Enter');
        }),
        opened.waitFor({ timeout: STEP_MS }),
    ]).catch(() => {});
    try {
        await opened.waitFor({ timeout: STEP_MS * 2 });
    } catch (error) {
        const banner = await store.locator('.store-banner').allInnerTexts().catch(() => []);
        throw new Error(`the Store did not install ${APP}: ${banner.join(' | ')}`, { cause: error });
    }
    log(`The Kazo OS Store installed ${APP}.`);
    await store.locator('xpath=ancestor::article[1]').locator('header.window-chrome .chrome-button-close').click();
}

// The Store reads the catalog on GitHub: go through the machine's proxy when there is one.
const proxy = process.env.HTTPS_PROXY ?? process.env.https_proxy;
if (fromStore && proxy) browserOptions.proxy = { server: proxy, bypass: '127.0.0.1,localhost' };
const browser = await chromium.launch(browserOptions);
const errors = [];
let page;
try {
    const context = await browser.newContext({ viewport: { width: 1440, height: 900 }, ignoreHTTPSErrors: fromStore && Boolean(proxy) });
    // raw.githubusercontent.com, as kazopkg reads it (ADR 0182 of Kazo OS), answered locally.
    if (!fromStore) await context.route(new RegExp(`^https://raw\\.githubusercontent\\.com/${REPOSITORY}/kazoos-packages/`), async route => {
        const relative = decodeURIComponent(new URL(route.request().url()).pathname).split('/kazoos-packages/')[1];
        const file = path.join(packages, relative);
        const headers = { 'Access-Control-Allow-Origin': '*' };
        try {
            if (!file.startsWith(packages)) throw new Error('outside the packages folder');
            await route.fulfill({ status: 200, body: await readFile(file), headers: { ...headers, 'Content-Type': 'application/octet-stream' } });
            log(`served ${relative}`);
        } catch {
            await route.fulfill({ status: 404, headers });
        }
    });
    page = await context.newPage();
    page.on('pageerror', error => errors.push(String(error)));

    // First boot: the administrator is created.
    await page.goto(baseUrl, { waitUntil: 'domcontentloaded' });
    await page.getByRole('heading', { name: 'Create administrator' }).waitFor({ timeout: 180_000 });
    await page.getByLabel('Login name').fill(LOGIN);
    await page.getByLabel('Display name').fill('Neo');
    await page.getByLabel('Password', { exact: true }).fill(PASSWORD);
    await page.getByLabel('Confirm password').fill(PASSWORD);
    await page.getByRole('button', { name: 'Create local profile' }).click();
    await page.getByText('Applications', { exact: true }).waitFor({ timeout: STEP_MS });

    if (fromStore) {
        await installFromStore(page);
    } else {
        await installWithKazopkg(page);
    }

    // A PNG in the home folder, through the File Explorer's Upload (host files into the OS).
    const upload = path.join(await mkdtemp(path.join(os.tmpdir(), 'kazo-photo-')), 'landscape.png');
    await writeFile(upload, await landscapePng(page));
    await openApp(page, 'File Explorer');
    // The toolbar's Upload acts on the folder listed: wait for the home folder's listing first.
    await page.locator(`tr[data-item-path="${HOME}/Documents"]`).first().waitFor({ timeout: STEP_MS });
    const [chooser] = await Promise.all([
        page.waitForEvent('filechooser', { timeout: STEP_MS }),
        page.locator('label[for="fileInput"]').first().click(),
    ]);
    await chooser.setFiles(upload);
    const row = name => page.locator(`tr[data-item-path="${HOME}/${name}"]`).first();
    await row('landscape.png').waitFor({ timeout: STEP_MS });

    // Kazo Photo from the launcher: the first start downloads and compiles PhotoCraft.
    await openApp(page, APP);
    const first = await waitForNewWindow(page, []);
    const frame = await frameOf(page, first.id);
    const renderer = await frame.evaluate(() => {
        const canvas = document.getElementById('photocraft_canvas');
        return canvas.getContext('webgpu') ? 'WebGPU' : canvas.getContext('webgl2') ? 'WebGL 2' : 'none';
    });
    log(`First window: PhotoCraft shown after ${(first.ms / 1000).toFixed(1)} s (download and compile), renderer ${renderer}.`);

    // File › Open: the OS dialog; the PNG opens bound to its file.
    await menu(frame, 'file.open');
    const open = page.locator('section.file-dialog[role="dialog"]').last();
    await open.getByRole('heading', { name: 'Open images' }).waitFor({ timeout: STEP_MS });
    await open.getByText('landscape.png', { exact: true }).dblclick();
    await open.waitFor({ state: 'detached', timeout: STEP_MS });
    await waitForDocument(frame, doc => doc.path === 'landscape.png', 'landscape.png');
    await expectTitle(page, first.id, `landscape.png — ${APP}`);
    log('File › Open showed the OS dialog and opened landscape.png.');

    // An adjustment layer: the flat PNG can't hold it, so Save asks where to write a .psd.
    await execute(frame, 'layer.newAdjustmentLayer.vibrance');
    await waitForDocument(frame, doc => doc.dirty, 'modified');
    await menu(frame, 'file.save');
    await answerSaveDialog(page, SAVE_TITLE, 'landscape.psd');
    await expectTitle(page, first.id, `landscape.psd — ${APP}`);
    await waitForDocument(frame, doc => !doc.dirty, 'clean after the save');
    await row('landscape.psd').waitFor({ timeout: STEP_MS });
    log('The layered image was saved as landscape.psd through the OS dialog.');
    await windowById(page, first.id).locator('header.window-chrome .chrome-button-close').click();
    await windowById(page, first.id).waitFor({ state: 'detached', timeout: STEP_MS });

    // A double-click on the .psd in the File Explorer: the installed app's file type opens it.
    await row('landscape.psd').dblclick({ position: { x: 40, y: 10 } });
    const second = await waitForNewWindow(page, []);
    log(`Second window: PhotoCraft shown after ${(second.ms / 1000).toFixed(1)} s (PhotoCraft in the browser cache).`);
    await expectTitle(page, second.id, `landscape.psd — ${APP}`);
    const reopened = await frameOf(page, second.id);
    await waitForDocument(reopened, doc => doc.path === 'landscape.psd', 'landscape.psd');
    log(`A double-click on landscape.psd opened it in ${APP}.`);

    // A layer added and saved in place: the window is bound to landscape.psd, no dialog.
    await execute(reopened, 'layer.newAdjustmentLayer.hueSaturation');
    await waitForDocument(reopened, doc => doc.dirty, 'modified again');
    await expectTitle(page, second.id, `*landscape.psd — ${APP}`);
    await menu(reopened, 'file.save');
    await waitForDocument(reopened, doc => !doc.dirty, 'saved in place');
    await expectTitle(page, second.id, `landscape.psd — ${APP}`);
    if (await page.locator('section.file-dialog').count() > 0) throw new Error('saving in place showed a dialog');
    log(`${APP} saved landscape.psd in place.`);

    // The licenses came with the package.
    for (const file of ['legal/LICENSE.txt', 'legal/THIRD-PARTY-NOTICES.md', 'legal/THIRD-PARTY-NOTICES-fr.md', 'legal/photocraft/LICENSE-lucide.txt',
        'photocraft/NOTICE', 'photocraft/LICENSE-MIT', 'photocraft/LICENSE-APACHE', 'photocraft/ATTRIBUTION.md', 'host/fonts/roboto/OFL.txt']) {
        const status = await reopened.evaluate(async url => (await fetch(url)).status, new URL(`../${file}`, reopened.url()).href);
        if (status !== 200) throw new Error(`${file} is not in the installed package (${status})`);
    }
    log('The licenses are installed with the app.');

    if (shots) await import('./store-screenshots.mjs').then(module => module.takeScreenshots({ page, second: second.id, reopened, execute, menu, shoot, windowById, openApp, HOME }));

    if (errors.length > 0) throw new Error(`page errors: ${errors.join(' | ')}`);
    log('PASS');
} catch (error) {
    console.error(`[installed] FAIL: ${error.message}`);
    await page?.screenshot({ path: 'installed-failure.png' }).catch(() => {});
    process.exitCode = 1;
} finally {
    await browser.close();
    server.close();
}
