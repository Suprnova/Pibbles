import * as vscode from 'vscode';
import * as alternatives from './alternatives.ts';
import { clickColumn, endColumn, enterColumn, stepsIntoId, trailingId } from './lineId.ts';
import { openSpan } from './spans.ts';

/**
 * The editing aids for `.pib` files: the cursor steps around trailing line IDs, Enter and Tab edit a variation's
 * alternatives like a Markdown list, and typing `[/` closes the innermost open span. The logic lives in the modules this
 * file imports, which know nothing about VS Code; this file only connects them to the editor.
 */
export function activate(context: vscode.ExtensionContext): void {
    context.subscriptions.push(
        vscode.commands.registerCommand('pibbles.cursorEnd', () => cursorEnd(false)),
        vscode.commands.registerCommand('pibbles.cursorEndSelect', () => cursorEnd(true)),
        vscode.commands.registerCommand('pibbles.cursorRight', cursorRight),
        vscode.commands.registerCommand('pibbles.selectLineId', selectLineId),
        vscode.commands.registerCommand('pibbles.enter', enter),
        vscode.commands.registerCommand('pibbles.tab', tab),
        vscode.window.onDidChangeTextEditorSelection(onSelectionChanged),
        vscode.workspace.onDidChangeTextDocument(onTextChanged),
    );
}

/** Whether the cursor treats a trailing `#id:` as if it weren't there, the `pibbles.lineIds.skipWithCursor` setting. */
function skipsIds(): boolean {
    return vscode.workspace.getConfiguration('pibbles').get('lineIds.skipWithCursor', true);
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

/** Selects the trailing ID on each cursor's line, for the rare edit. */
function selectLineId(): void {
    const editor = vscode.window.activeTextEditor;
    if (!editor) {
        return;
    }

    editor.selections = editor.selections.map(selection => {
        const id = trailingId(lineAt(editor, selection.active.line));
        return id ? new vscode.Selection(selection.active.line, id.idStart, selection.active.line, id.idEnd) : selection;
    });
}

/** A click past the end of a line puts the cursor before its ID. */
function onSelectionChanged(event: vscode.TextEditorSelectionChangeEvent): void {
    const editor = event.textEditor;
    if (event.kind !== vscode.TextEditorSelectionChangeKind.Mouse || editor.document.languageId !== 'pibbles' || !skipsIds()) {
        return;
    }

    const moved = event.selections.map(selection => {
        if (!selection.isEmpty) {
            return selection;
        }

        const column = clickColumn(lineAt(editor, selection.active.line), selection.active.character);
        const active = selection.active.with({ character: column });
        return new vscode.Selection(active, active);
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

function lineAt(editor: vscode.TextEditor, index: number): string {
    return editor.document.lineAt(index).text;
}

function lines(editor: vscode.TextEditor): string[] {
    return editor.document.getText().split(/\r?\n/);
}
