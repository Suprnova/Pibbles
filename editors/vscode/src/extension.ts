import * as vscode from 'vscode';
import * as alternatives from './alternatives.ts';
import { clickColumn, endColumn, enterColumn, hiddenColumn, isLineId, joinLines, selectsOnlyId, stepsIntoId, trailingId } from './lineId.ts';
import { openSpan } from './spans.ts';

/**
 * The editing aids for `.pib` files: trailing line IDs are hidden, shown for the cursor's line in the status bar, and
 * stepped around by the cursor; Enter and Tab edit a variation's alternatives like a Markdown list; and typing `[/`
 * closes the innermost open span. The logic lives in the modules this file imports, which know nothing about VS Code;
 * this file only connects them to the editor.
 */
export function activate(context: vscode.ExtensionContext): void {
    // Zero-size text, not `display: none`, so the editor can still measure the hidden characters to draw cursors and
    // selections. Any `letterSpacing` makes it measure them rather than assume a fixed width per character.
    const hidden = vscode.window.createTextEditorDecorationType({ textDecoration: 'none; font-size: 0', letterSpacing: '0' });
    const status = vscode.window.createStatusBarItem('pibbles.lineId', vscode.StatusBarAlignment.Right, 100);
    status.name = 'Pibbles Line ID';
    status.tooltip = 'Saves, translations and recordings find this line by its ID. Click to edit it.';
    status.command = 'pibbles.editLineId';

    const hideAll = () => vscode.window.visibleTextEditors.forEach(editor => hideIds(editor, hidden));
    const show = () => showId(status);
    hideAll();
    show();

    context.subscriptions.push(
        hidden,
        status,
        vscode.commands.registerCommand('pibbles.cursorEnd', () => cursorEnd(false)),
        vscode.commands.registerCommand('pibbles.cursorEndSelect', () => cursorEnd(true)),
        vscode.commands.registerCommand('pibbles.cursorRight', cursorRight),
        vscode.commands.registerCommand('pibbles.deleteLeft', () => deleteJoining('deleteLeft')),
        vscode.commands.registerCommand('pibbles.deleteRight', () => deleteJoining('deleteRight')),
        vscode.commands.registerCommand('pibbles.editLineId', editLineId),
        vscode.commands.registerCommand('pibbles.enter', enter),
        vscode.commands.registerCommand('pibbles.tab', tab),
        vscode.window.onDidChangeTextEditorSelection(onSelectionChanged),
        vscode.window.onDidChangeTextEditorSelection(show),
        vscode.window.onDidChangeActiveTextEditor(show),
        vscode.window.onDidChangeVisibleTextEditors(hideAll),
        vscode.workspace.onDidChangeTextDocument(onTextChanged),
        vscode.workspace.onDidChangeTextDocument(event => {
            noteTypedId(event);
            vscode.window.visibleTextEditors.filter(editor => editor.document === event.document).forEach(editor => hideIds(editor, hidden));
            show();
        }),
        vscode.window.onDidChangeTextEditorSelection(event => {
            if (typedId?.document === event.textEditor.document && event.textEditor.selection.active.line !== typedId.line) {
                typedId = undefined;
                hideIds(event.textEditor, hidden);
            }
        }),
        vscode.workspace.onDidChangeConfiguration(event => {
            if (event.affectsConfiguration('pibbles.lineIds')) {
                hideAll();
            }
        }),
    );
}

/** The line whose ID is being typed, which stays shown, with the cursor free to move in it, until the cursor leaves. */
let typedId: { document: vscode.TextDocument; line: number } | undefined;

/** Notes a change made inside a line's trailing ID, such as typing one, so the ID isn't hidden from under the cursor. */
function noteTypedId(event: vscode.TextDocumentChangeEvent): void {
    const change = event.contentChanges.length === 1 ? event.contentChanges[0] : undefined;
    if (event.document.languageId !== 'pibbles' || !change?.range.isSingleLine || change.text.includes('\n')) {
        return;
    }

    const line = change.range.start.line;
    const id = trailingId(event.document.lineAt(line).text);
    if (id && change.range.start.character > id.contentEnd) {
        typedId = { document: event.document, line };
    }
}

function isTypedId(editor: vscode.TextEditor, line: number): boolean {
    return typedId?.document === editor.document && typedId.line === line;
}

/** Whether trailing IDs are hidden, the `pibbles.lineIds.hide` setting. */
function hidesIds(): boolean {
    return vscode.workspace.getConfiguration('pibbles').get('lineIds.hide', true);
}

/**
 * Whether the cursor treats a trailing `#id:` as if it weren't there: the `pibbles.lineIds.skipWithCursor` setting, and
 * always while IDs are hidden.
 */
function skipsIds(): boolean {
    return vscode.workspace.getConfiguration('pibbles').get('lineIds.skipWithCursor', true) || hidesIds();
}

/**
 * Hides each trailing ID in a `.pib` editor, with the space before it and any whitespace after it, except the one being
 * typed. Or shows them all again.
 */
function hideIds(editor: vscode.TextEditor, hidden: vscode.TextEditorDecorationType): void {
    if (!isPibbles(editor)) {
        return;
    }

    const ranges = hidesIds()
        ? lines(editor).flatMap((text, index) => {
            const id = trailingId(text);
            return id && !isTypedId(editor, index) ? [new vscode.Range(index, id.contentEnd, index, text.length)] : [];
        })
        : [];
    editor.setDecorations(hidden, ranges);
}

/** Shows the ID of the active editor's cursor line in the status bar, if it has one. */
function showId(status: vscode.StatusBarItem): void {
    const editor = vscode.window.activeTextEditor;
    const id = editor && isPibbles(editor) ? trailingId(lineAt(editor, editor.selection.active.line)) : undefined;
    if (!id) {
        status.hide();
        return;
    }

    status.text = `$(tag) ${id.value}`;
    status.show();
}

/** **End** and **Shift+End**: VS Code's own, then a cursor that went from the content past it stops before the ID. */
async function cursorEnd(select: boolean): Promise<void> {
    const editor = vscode.window.activeTextEditor;
    if (!editor) {
        return;
    }

    const before = editor.selections;
    await vscode.commands.executeCommand(select ? 'cursorEndSelect' : 'cursorEnd');
    editor.selections = editor.selections.map((after, i) => {
        const was = before[i]?.active;
        if (!was || was.line !== after.active.line) {
            return after;
        }

        const active = after.active.with({ character: endColumn(lineAt(editor, was.line), was.character, after.active.character) });
        return new vscode.Selection(select ? after.anchor : active, active);
    });
}

/** **Right**: VS Code's own, except that from the content's end it goes to the next line, as from a line's real end. */
async function cursorRight(): Promise<void> {
    const editor = vscode.window.activeTextEditor;
    if (!editor) {
        return;
    }

    const before = editor.selections;
    await vscode.commands.executeCommand('cursorRight');
    editor.selections = editor.selections.map((after, i) => {
        const was = before[i];
        if (!was?.isEmpty || !stepsIntoId(lineAt(editor, was.active.line), was.active.character) || was.active.line + 1 >= editor.document.lineCount) {
            return after;
        }

        const next = new vscode.Position(was.active.line + 1, 0);
        return new vscode.Selection(next, next);
    });
}

/**
 * **Backspace** and **Delete**: VS Code's own, except where they join lines, which they do so that each ID stays with
 * its line. Delete at the content's end joins the next line, as from the line's real end.
 */
async function deleteJoining(command: 'deleteLeft' | 'deleteRight'): Promise<void> {
    const editor = vscode.window.activeTextEditor;
    const range = editor && joinedRange(editor, command);
    const joined = range && joinLines(lineAt(editor, range.start.line), range.start.character, lineAt(editor, range.end.line), range.end.character);
    if (!joined) {
        await vscode.commands.executeCommand(command);
        return;
    }

    await editor.edit(edit => edit.replace(new vscode.Range(range.start.line, 0, range.end.line, lineAt(editor, range.end.line).length), joined.text));
    const cursor = new vscode.Position(range.start.line, joined.cursor);
    editor.selection = new vscode.Selection(cursor, cursor);
}

/** What a deletion removes, if it joins lines: a selection across lines, or the line break next to the cursor. */
function joinedRange(editor: vscode.TextEditor, command: 'deleteLeft' | 'deleteRight'): vscode.Range | undefined {
    const selection = editor.selection;
    if (!selection.isEmpty) {
        return selection.isSingleLine ? undefined : selection;
    }

    const { line, character } = selection.active;
    if (command === 'deleteLeft') {
        return character === 0 && line > 0 ? new vscode.Range(line - 1, lineAt(editor, line - 1).length, line, 0) : undefined;
    }

    const text = lineAt(editor, line);
    const atEnd = character === text.length || stepsIntoId(text, character);
    return atEnd && line + 1 < editor.document.lineCount ? new vscode.Range(line, text.length, line + 1, 0) : undefined;
}

/** Edits the ID on the cursor's line, for the rare edit: a pasted duplicate, or a line the game refers to by name. */
async function editLineId(): Promise<void> {
    const editor = vscode.window.activeTextEditor;
    if (!editor) {
        return;
    }

    const index = editor.selection.active.line;
    const id = trailingId(lineAt(editor, index));
    if (!id) {
        void vscode.window.showInformationMessage('This line has no ID. Lines that need one get it from `pibbles ids`.');
        return;
    }

    const version = editor.document.version;
    const value = await vscode.window.showInputBox({
        title: 'Edit Line ID',
        value: id.value,
        prompt: 'Saves, translations and recordings find this line by its ID, so a released line keeps it.',
        validateInput: text => isLineId(text) ? undefined : 'A line ID is a lowercase letter, then lowercase letters, digits and _.',
    });

    if (value !== undefined && value !== id.value && editor.document.version === version) {
        await editor.edit(edit => edit.replace(new vscode.Range(index, id.idEnd - id.value.length, index, id.idEnd), value));
    }
}

/**
 * Keeps cursors and selections out of hidden IDs, and with IDs shown, puts a click past the end of a line before its ID.
 */
function onSelectionChanged(event: vscode.TextEditorSelectionChangeEvent): void {
    const editor = event.textEditor;
    const hidden = hidesIds();
    if (!isPibbles(editor) || !hidden && (event.kind !== vscode.TextEditorSelectionChangeKind.Mouse || !skipsIds())) {
        return;
    }

    const moved = event.selections.map(selection => {
        if (!hidden && !selection.isEmpty) {
            return selection;
        }

        const isCursor = selection.isSingleLine && selectsOnlyId(lineAt(editor, selection.start.line), selection.start.character);
        const place = (position: vscode.Position) => {
            if (hidden && isTypedId(editor, position.line)) {
                return position;
            }

            const line = lineAt(editor, position.line);
            const column = hidden ? hiddenColumn(line, position.character, selection.isEmpty || isCursor) : clickColumn(line, position.character);
            return position.with({ character: column });
        };
        return new vscode.Selection(place(selection.anchor), place(selection.active));
    });

    if (moved.some((selection, i) => !selection.isEqual(event.selections[i]))) {
        editor.selections = moved;
    }
}

/**
 * **Enter**: continues or leaves a variation's alternatives, and otherwise breaks the line as VS Code does, after any
 * trailing ID the cursor is just before, so the ID stays on its line.
 */
async function enter(): Promise<void> {
    const editor = vscode.window.activeTextEditor;
    if (!editor) {
        return;
    }

    const position = editor.selection.active;
    const line = lineAt(editor, position.line);
    const action = alternatives.enter(lines(editor), position.line, position.character);

    if (action?.kind === 'continue') {
        const end = new vscode.Position(position.line, line.length);
        await editor.edit(edit => edit.insert(end, `\n${action.newLine}`));
        const next = new vscode.Position(position.line + 1, action.newLine.length);
        editor.selection = new vscode.Selection(next, next);
    } else if (action?.kind === 'leave') {
        await editor.edit(edit => edit.replace(editor.document.lineAt(position.line).range, action.line));
        const end = new vscode.Position(position.line, action.line.length);
        editor.selection = new vscode.Selection(end, end);
    } else {
        if (skipsIds()) {
            const column = enterColumn(line, position.character);
            editor.selection = new vscode.Selection(position.line, column, position.line, column);
        }

        await vscode.commands.executeCommand('type', { source: 'keyboard', text: '\n' });
    }
}

/** **Tab**: a line holding only `- ` becomes a continuation of the alternative above it. Otherwise, VS Code's own Tab. */
async function tab(): Promise<void> {
    const editor = vscode.window.activeTextEditor;
    if (!editor) {
        return;
    }

    const index = editor.selection.active.line;
    const unit = editor.options.insertSpaces ? ' '.repeat(Number(editor.options.tabSize)) : '\t';
    const replaced = alternatives.tab(lines(editor), index, unit);
    if (replaced === undefined) {
        await vscode.commands.executeCommand('tab');
        return;
    }

    await editor.edit(edit => edit.replace(editor.document.lineAt(index).range, replaced));
    const end = new vscode.Position(index, replaced.length);
    editor.selection = new vscode.Selection(end, end);
}

/** Reacts to typing: `- ` on a continuation line moves out to the alternatives, and `[/` closes the open span. */
function onTextChanged(event: vscode.TextDocumentChangeEvent): void {
    const editor = vscode.window.activeTextEditor;
    if (editor?.document !== event.document || event.document.languageId !== 'pibbles' || event.reason !== undefined || event.contentChanges.length !== 1) {
        return;
    }

    const change = event.contentChanges[0];
    const index = change.range.start.line;
    if (change.text === ' ') {
        const moved = alternatives.dashTyped(lines(editor), index);
        if (moved !== undefined) {
            void replaceLine(editor, index, moved, moved.length);
        }
    } else if (change.text === '/') {
        closeSpan(editor, change.range.start);
    }
}

/** Completes `[/` to close the innermost open span, keeping an auto-closed `]` if VS Code added one. */
function closeSpan(editor: vscode.TextEditor, slash: vscode.Position): void {
    const line = lineAt(editor, slash.line);
    if (line[slash.character - 1] !== '[') {
        return;
    }

    const name = openSpan(line.slice(0, slash.character - 1));
    if (name === undefined) {
        return;
    }

    const after = new vscode.Position(slash.line, slash.character + 1);
    const closed = line[slash.character + 1] === ']';
    void editor.edit(edit => edit.insert(after, closed ? name : `${name}]`), { undoStopBefore: false, undoStopAfter: false }).then(() => {
        const end = after.translate(0, name.length + 1);
        editor.selection = new vscode.Selection(end, end);
    });
}

async function replaceLine(editor: vscode.TextEditor, index: number, text: string, cursor: number): Promise<void> {
    await editor.edit(edit => edit.replace(editor.document.lineAt(index).range, text), { undoStopBefore: false, undoStopAfter: false });
    const position = new vscode.Position(index, cursor);
    editor.selection = new vscode.Selection(position, position);
}

function isPibbles(editor: vscode.TextEditor): boolean {
    return editor.document.languageId === 'pibbles';
}

function lineAt(editor: vscode.TextEditor, index: number): string {
    return editor.document.lineAt(index).text;
}

function lines(editor: vscode.TextEditor): string[] {
    return editor.document.getText().split(/\r?\n/);
}
