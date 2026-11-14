// Shared helpers of the end-to-end runs: the browser, the Kazo Photo windows and what runs in
// their iframes (PhotoCraft's page).
import { chromium } from 'playwright';
import { boot } from './host.mjs';

// PhotoCraft's first start downloads about 40 MB of .wasm from the local host and compiles it.
export const START_TIMEOUT_MS = 120_000;

// A software WebGPU adapter (SwiftShader), so both of PhotoCraft's renderers can be tried.
export const launchBrowser = () => chromium.launch({
    args: ['--enable-unsafe-webgpu', '--enable-features=Vulkan', '--use-vulkan=swiftshader', '--use-webgpu-adapter=swiftshader', '--use-angle=vulkan'],
});

/** A fresh page with the OS booted; `errors` collects the page's uncaught errors. */
export async function bootPage(browser, baseUrl) {
    const page = await browser.newPage({ viewport: { width: 1400, height: 900 } });
    const errors = [];
    page.on('pageerror', error => errors.push(error.message));
    if (process.env.PHOTO_E2E_CONSOLE) {
        const { appendFileSync } = await import('node:fs');
        page.on('console', message => message.type() === 'error' && appendFileSync(process.env.PHOTO_E2E_CONSOLE, `${message.text()}\n----\n`));
    }
    await boot(page, baseUrl);
    return { page, errors };
}

/** Kazo Photo's OS windows, in opening order. */
export const photoWindows = page => page.locator('article[data-window-id]', { has: page.locator('.photo-window') });

/** The iframe element of the window at `index` (opening order). */
export const iframeAt = (page, index) => photoWindows(page).nth(index).locator('iframe.craft-frame-iframe');

/** The PhotoCraft frame of the window at `index`, once the module put the iframe there. */
export async function frameAt(page, index) {
    const iframe = iframeAt(page, index);
    await iframe.waitFor({ state: 'attached', timeout: START_TIMEOUT_MS });
    return (await iframe.elementHandle()).contentFrame();
}

/**
 * Waits until PhotoCraft runs in a frame: its start code removes #photocraft_loading once eframe
 * started on the canvas (apps/photocraft-web/src/web.rs), and the canvas then has a size.
 */
export async function waitForPhotoCraft(frame) {
    await frame.waitForFunction(() => {
        const canvas = document.getElementById('photocraft_canvas');
        return document.getElementById('photocraft_loading') === null && canvas !== null && canvas.width > 0;
    }, undefined, { timeout: START_TIMEOUT_MS });
}

/** The renderer PhotoCraft's canvas uses: `webgpu`, `webgl2` or null. */
export const rendererOf = frame => frame.evaluate(() => {
    const canvas = document.getElementById('photocraft_canvas');
    if (canvas.getContext('webgpu')) {
        return 'webgpu';
    }
    return canvas.getContext('webgl2') ? 'webgl2' : null;
});

// The steps of a run that waits on the OS or on PhotoCraft, once PhotoCraft runs.
export const STEP_TIMEOUT_MS = 30_000;
export const HOME = '/home/neo';

/** Saves a screenshot of the page when PHOTO_E2E_SHOTS names a folder. */
export async function shot(page, name) {
    if (process.env.PHOTO_E2E_SHOTS) {
        await page.screenshot({ path: `${process.env.PHOTO_E2E_SHOTS}/${name}.png` });
    }
}

/** One OS window by its id. */
export const windowById = (page, id) => page.locator(`article[data-window-id="${id}"]`);

/** Waits for `count` Kazo Photo windows to show PhotoCraft; returns the id of the newest one. */
export async function waitForWindow(page, count, known = []) {
    try {
        await page.locator('.photo-frame.running').nth(count - 1).waitFor({ timeout: START_TIMEOUT_MS });
    } catch (error) {
        await shot(page, `window-${count}`);
        throw error;
    }
    const ids = await photoWindows(page).evaluateAll(nodes => nodes.map(node => node.getAttribute('data-window-id')));
    if (ids.length !== count) {
        throw new Error(`${ids.length} Kazo Photo windows instead of ${count}`);
    }
    return ids.find(id => !known.includes(id));
}

/** The PhotoCraft frame of a window, by the window's id. */
export async function frameOf(page, id) {
    const handle = await windowById(page, id).locator('iframe.craft-frame-iframe').elementHandle();
    return handle.contentFrame();
}

/** Runs a method of PhotoCraft's control API (window.photocraft, host mode) in a frame. */
export const request = (frame, method, params = {}) =>
    frame.evaluate(([name, args]) => window.photocraft.request(name, args), [method, params]);

/** Runs a PhotoCraft engine command, as its menus do. */
export const execute = (frame, command, params = {}) => request(frame, 'engine.execute', { command, params });

/** Runs a PhotoCraft menu item (File › Save…), which opens what the menu opens. */
export const menu = (frame, id) => request(frame, 'ui.menu.invoke', { id });

/** PhotoCraft's open documents (session.inspect). */
export async function documentsOf(frame) {
    const answer = await execute(frame, 'session.inspect');
    return answer?.documents ?? answer?.result?.documents ?? [];
}

/** Waits until one of the frame's documents matches `predicate` (a function of a session.inspect document). */
export async function waitForDocument(frame, predicate, description) {
    const deadline = Date.now() + STEP_TIMEOUT_MS;
    let last = [];
    while (Date.now() < deadline) {
        last = await documentsOf(frame).catch(error => [String(error)]);
        if (last.some(doc => typeof doc === 'object' && predicate(doc))) {
            return;
        }
        await new Promise(resolve => setTimeout(resolve, 200));
    }
    throw new Error(`no document ${description}: ${JSON.stringify(last)}`);
}

/** Creates a small image and adds a layer, so that it has unsaved changes a PNG cannot hold. */
export async function newModifiedImage(frame) {
    await execute(frame, 'file.new', { width: 64, height: 48 });
    await execute(frame, 'layer.new.layer');
    await waitForDocument(frame, doc => doc.dirty, 'modified after file.new');
}

/** Answers the OS save dialog with a file name (in the folder it opens in). */
export async function answerSaveDialog(page, title, fileName) {
    const dialog = page.locator('section.file-dialog', { hasText: title });
    await dialog.waitFor({ state: 'visible', timeout: STEP_TIMEOUT_MS });
    await dialog.getByLabel('File name').fill(fileName);
    await dialog.getByRole('button', { name: 'Save' }).click();
    await dialog.waitFor({ state: 'hidden', timeout: STEP_TIMEOUT_MS });
}

/** Waits for a window's title bar to read `text`; on failure, says what it reads and the window's notice. */
export async function expectTitle(page, id, text) {
    try {
        await page.waitForFunction(
            ([windowId, wanted]) => document.querySelector(`article[data-window-id="${windowId}"] header.window-chrome h2`)?.textContent?.trim() === wanted,
            [id, text],
            { timeout: STEP_TIMEOUT_MS });
    } catch (error) {
        const actual = await windowById(page, id).locator('header.window-chrome h2').first().textContent().catch(() => null);
        const notice = await windowById(page, id).locator('.photo-notice').textContent().catch(() => null);
        await shot(page, `title-${id}`);
        throw new Error(`title "${actual}" instead of "${text}"; notice: ${notice}`, { cause: error });
    }
}
