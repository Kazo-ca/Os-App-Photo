// End-to-end runs of opening, saving, exporting and closing (doc/en/plan.md, work item 2,
// decisions 6 to 8), inside the Kazo OS host: `npm run test:e2e` after
// `dotnet build Kazo.Photo.slnx -c Debug`.
//
// PhotoCraft is driven through its page API (window.photocraft in the iframe) to create an image
// and run its menu items, as its menus and shortcuts do; everything else goes through the OS: its
// save and open dialogs, its message box, the window's title bar and close button, and the
// Terminal (`xdg-open` starts the app associated with the file type). PhotoCraft starts with
// --webgl: Chromium's software WebGPU adapter loses its device in headless runs (photo.e2e.mjs).
import { after, before, test } from 'node:test';
import assert from 'node:assert/strict';
import { runInTerminal, startHost } from './host.mjs';
import {
    HOME, STEP_TIMEOUT_MS, answerSaveDialog, bootPage, documentsOf, execute, expectTitle, frameOf, launchBrowser, menu, newModifiedImage,
    photoWindows, shot, waitForDocument, waitForWindow, windowById,
} from './common.mjs';

const SAVE_TITLE = 'Save the image';
const COPY_TITLE = 'Save a copy';

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

/** Opens Kazo Photo on WebGL 2 from the Terminal; returns its window id and PhotoCraft's frame. */
async function launch(page, known = [], argument = '') {
    await runInTerminal(page, `launch ca.kazo.photo --webgl ${argument}`.trim());
    const id = await waitForWindow(page, known.length + 1, known);
    return { id, frame: await frameOf(page, id) };
}

/** Waits for the Terminal's output to show `text`. */
async function expectInTerminal(page, text) {
    await page.locator('.xterm-rows').last().getByText(text, { exact: false }).first().waitFor({ timeout: STEP_TIMEOUT_MS });
}

/**
 * Lets an OS file dialog finish watching the folder it just entered before it closes: Kazo OS's
 * file view throws when it is disposed while that watch starts (FileView.AttachWatchAsync leaves a
 * disposed CancellationTokenSource that DisposeAsync cancels), and Blazor then stops rendering.
 * A Kazo OS bug, outside Kazo Photo; a person never picks a file that fast.
 */
const settleAfterNavigation = page => page.waitForTimeout(1_000);

/** A new image saved as `fileName` in the home folder, through File › Save and the OS dialog. */
async function saveNewImage(page, id, frame, fileName) {
    await newModifiedImage(frame);
    await menu(frame, 'file.save');
    await answerSaveDialog(page, SAVE_TITLE, fileName);
    await expectTitle(page, id, `${fileName} — Kazo Photo`);
    await waitForDocument(frame, doc => !doc.dirty, 'clean after the save');
}

test('a new image is saved where the user says, saved again in place, and reopens from the OS', { timeout: 400_000 }, async () => {
    const { page, errors } = await bootPage(browser, host.baseUrl);
    const { id, frame } = await launch(page);

    await newModifiedImage(frame);
    await expectTitle(page, id, '*Untitled — Kazo Photo');

    // The first save of a new image asks where to write it.
    await menu(frame, 'file.save');
    await answerSaveDialog(page, SAVE_TITLE, 'holiday.psd');
    await expectTitle(page, id, 'holiday.psd — Kazo Photo');
    await waitForDocument(frame, doc => !doc.dirty, 'clean after the save');

    // The next save writes the same file, with no dialog.
    await execute(frame, 'layer.new.layer');
    await expectTitle(page, id, '*holiday.psd — Kazo Photo');
    await menu(frame, 'file.save');
    await expectTitle(page, id, 'holiday.psd — Kazo Photo');
    assert.equal(await page.locator('section.file-dialog').count(), 0);

    // `xdg-open` starts the app associated with .psd: a second window, on the file.
    await runInTerminal(page, `xdg-open ${HOME}/holiday.psd`);
    const second = await waitForWindow(page, 2, [id]);
    await expectTitle(page, second, 'holiday.psd — Kazo Photo');
    await waitForDocument(await frameOf(page, second), doc => doc.path === 'holiday.psd', 'holiday.psd');

    assert.deepEqual(errors, []);
    await page.close();
});

test('Save As asks for a new file and binds the image to it; a copy leaves the image as it is', { timeout: 400_000 }, async () => {
    const { page, errors } = await bootPage(browser, host.baseUrl);
    const { id, frame } = await launch(page);
    await saveNewImage(page, id, frame, 'first.psd');

    await menu(frame, 'file.saveAs');
    await answerSaveDialog(page, SAVE_TITLE, 'second.psd');
    await expectTitle(page, id, 'second.psd — Kazo Photo');

    // A plain save now writes second.psd in place.
    await execute(frame, 'layer.new.layer');
    await expectTitle(page, id, '*second.psd — Kazo Photo');
    await menu(frame, 'file.save');
    await expectTitle(page, id, 'second.psd — Kazo Photo');
    assert.equal(await page.locator('section.file-dialog').count(), 0);

    // Save a Copy is an export: a copy where the user says, the image stays bound to second.psd.
    await execute(frame, 'layer.new.layer');
    await expectTitle(page, id, '*second.psd — Kazo Photo');
    await menu(frame, 'file.saveACopy');
    await answerSaveDialog(page, COPY_TITLE, 'copy.psd');
    await windowById(page, id).locator('.photo-notice', { hasText: 'Copy saved: copy.psd' }).waitFor({ timeout: STEP_TIMEOUT_MS });
    await expectTitle(page, id, '*second.psd — Kazo Photo');

    await runInTerminal(page, `ls ${HOME}`);
    await expectInTerminal(page, 'copy.psd');
    const listing = await page.locator('.xterm-rows').last().innerText();
    assert.match(listing, /first\.psd/);
    assert.match(listing, /second\.psd/);

    assert.deepEqual(errors, []);
    await page.close();
});

test('closing with changes asks; Cancel keeps the window, Yes saves before closing', { timeout: 400_000 }, async () => {
    const { page, errors } = await bootPage(browser, host.baseUrl);
    const { id, frame } = await launch(page);
    await newModifiedImage(frame);
    await expectTitle(page, id, '*Untitled — Kazo Photo');

    const close = windowById(page, id).locator('header.window-chrome .chrome-button-close');
    const question = page.locator('.message-box-dialog', { hasText: 'Save the changes' });

    // Cancel: nothing closes, nothing is lost.
    await close.click();
    await question.waitFor({ timeout: STEP_TIMEOUT_MS });
    await question.getByRole('button', { name: 'Cancel' }).click();
    await question.waitFor({ state: 'hidden', timeout: STEP_TIMEOUT_MS });
    assert.equal(await windowById(page, id).count(), 1);
    await waitForDocument(frame, doc => doc.dirty, 'still modified');

    // Yes, then Cancel in the save dialog: the window stays open, and the notice says so.
    await close.click();
    await question.waitFor({ timeout: STEP_TIMEOUT_MS });
    await question.getByRole('button', { name: 'Yes' }).click();
    const saveDialog = page.locator('section.file-dialog', { hasText: SAVE_TITLE });
    await saveDialog.waitFor({ timeout: STEP_TIMEOUT_MS });
    await saveDialog.getByRole('button', { name: 'Cancel' }).click();
    await windowById(page, id).locator('.photo-notice', { hasText: 'was not saved' }).waitFor({ timeout: STEP_TIMEOUT_MS });
    assert.equal(await windowById(page, id).count(), 1);
    await waitForDocument(frame, doc => doc.dirty, 'still modified after the cancelled save');

    // Yes, then a file name: the image is written, then the window closes.
    await close.click();
    await question.waitFor({ timeout: STEP_TIMEOUT_MS });
    await question.getByRole('button', { name: 'Yes' }).click();
    await answerSaveDialog(page, SAVE_TITLE, 'closing.psd');
    await windowById(page, id).waitFor({ state: 'detached', timeout: STEP_TIMEOUT_MS });

    await runInTerminal(page, `xdg-open ${HOME}/closing.psd`);
    const reopened = await waitForWindow(page, 1);
    await expectTitle(page, reopened, 'closing.psd — Kazo Photo');

    assert.deepEqual(errors, []);
    await page.close();
});

test('PhotoCraft\'s Open shows the OS dialog, the picked file is bound, and a second open brings its tab to the front', { timeout: 400_000 }, async () => {
    const { page, errors } = await bootPage(browser, host.baseUrl);
    const { id, frame } = await launch(page);
    await saveNewImage(page, id, frame, 'picked.psd');
    await windowById(page, id).locator('header.window-chrome .chrome-button-close').click();
    await windowById(page, id).waitFor({ state: 'detached', timeout: STEP_TIMEOUT_MS });

    const fresh = await launch(page);
    const pick = async () => {
        await menu(fresh.frame, 'file.open');
        const dialog = page.locator('section.file-dialog[role="dialog"]').last();
        await dialog.getByRole('heading', { name: 'Open images' }).waitFor({ timeout: STEP_TIMEOUT_MS });
        await dialog.getByText('picked.psd', { exact: true }).dblclick().catch(async error => {
            await shot(page, 'open-dialog');
            throw error;
        });
        await dialog.waitFor({ state: 'detached', timeout: STEP_TIMEOUT_MS });
    };

    await pick();
    await waitForDocument(fresh.frame, doc => doc.path === 'picked.psd', 'picked.psd');
    await expectTitle(page, fresh.id, 'picked.psd — Kazo Photo');

    // A new image in front, then picked.psd again: its tab comes back, no second copy opens.
    await newModifiedImage(fresh.frame);
    await expectTitle(page, fresh.id, '*Untitled — Kazo Photo');
    await pick();
    await expectTitle(page, fresh.id, '*picked.psd — Kazo Photo');
    const documents = await documentsOf(fresh.frame);
    assert.equal(documents.filter(doc => doc.path === 'picked.psd').length, 1);
    assert.equal(await photoWindows(page).count(), 1);

    assert.deepEqual(errors, []);
    await page.close();
});

test('a file named like an open image of another folder opens in a new window', { timeout: 500_000 }, async () => {
    const { page, errors } = await bootPage(browser, host.baseUrl);
    await runInTerminal(page, `mkdir ${HOME}/Work`);
    const { id, frame } = await launch(page);
    await saveNewImage(page, id, frame, 'poster.psd');

    // A second poster.psd, in Work: written by Save a Copy, so the first window keeps its file.
    await execute(frame, 'layer.new.layer');
    await menu(frame, 'file.saveACopy');
    const dialog = page.locator('section.file-dialog', { hasText: COPY_TITLE });
    await dialog.waitFor({ timeout: STEP_TIMEOUT_MS });
    await dialog.getByText('Work', { exact: true }).dblclick();
    await dialog.getByText('This folder is empty.').waitFor({ timeout: STEP_TIMEOUT_MS });
    await settleAfterNavigation(page);
    await dialog.getByLabel('File name').fill('poster.psd');
    await dialog.getByRole('button', { name: 'Save' }).click();
    await dialog.waitFor({ state: 'hidden', timeout: STEP_TIMEOUT_MS }).catch(async error => {
        await shot(page, 'copy-dialog');
        throw error;
    });
    await windowById(page, id).locator('.photo-notice', { hasText: 'Copy saved' }).waitFor({ timeout: STEP_TIMEOUT_MS });

    // Decision 7: PhotoCraft would take two tabs named poster.psd for one file.
    await menu(frame, 'file.open');
    const open = page.locator('section.file-dialog[role="dialog"]').last();
    await open.getByRole('heading', { name: 'Open images' }).waitFor({ timeout: STEP_TIMEOUT_MS });
    await open.getByText('Work', { exact: true }).dblclick();
    await open.getByText('poster.psd', { exact: true }).waitFor({ timeout: STEP_TIMEOUT_MS });
    await settleAfterNavigation(page);
    await open.getByText('poster.psd', { exact: true }).dblclick();
    await open.waitFor({ state: 'detached', timeout: STEP_TIMEOUT_MS });

    const second = await waitForWindow(page, 2, [id]);
    await expectTitle(page, second, 'poster.psd — Kazo Photo');
    assert.equal((await documentsOf(frame)).length, 1);

    assert.deepEqual(errors, []);
    await page.close();
});

test('an OS chord pressed in the image reaches the OS: Alt+Q closes through the close guard', { timeout: 400_000 }, async () => {
    const { page, errors } = await bootPage(browser, host.baseUrl);
    const { id, frame } = await launch(page);
    await newModifiedImage(frame);
    await expectTitle(page, id, '*Untitled — Kazo Photo');

    // The keyboard is in PhotoCraft's canvas, as when the user works on the image.
    await frame.locator('#photocraft_canvas').click({ position: { x: 600, y: 400 } });
    // Alt+Q is the OS's other Close chord (WindowKeyBindings); headless Chromium never delivers the
    // F4 of Alt+F4, from the image or from the OS page.
    await page.keyboard.press('Alt+KeyQ');
    const question = page.locator('.message-box-dialog', { hasText: 'Save the changes' });
    await question.waitFor({ timeout: STEP_TIMEOUT_MS });
    await question.getByRole('button', { name: 'No' }).click();
    await windowById(page, id).waitFor({ state: 'detached', timeout: STEP_TIMEOUT_MS });

    assert.deepEqual(errors, []);
    await page.close();
});
