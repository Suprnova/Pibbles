import { trailingId } from './lineId.ts';

/**
 * Editing a variation's alternatives like a Markdown list. An alternative is a line starting with `- `, or a `-` on its
 * own, directly in a `@sequence` or `@cycle` block. A `- ` line anywhere else is text, and these rules leave it alone.
 */

const Variation = /^\s*@(sequence|cycle)\b/;
const Dash = /^-(\s|$)/;
const EmptyAlternative = /^-[ \t]+$/;

/** What to do when Enter is pressed on a line, if it's an alternative. */
export type EnterAction =
    /** Start the next alternative: insert a new line with this text. */
    | { kind: 'continue'; newLine: string }
    /** Leave the variation: replace the line, which held only `- `, with this text. */
    | { kind: 'leave'; line: string };

/**
 * What Enter does at `column` of line `index`, or `undefined` for VS Code's usual Enter. At the end of an alternative's
 * content, before any trailing ID, it starts the next one. On a line holding only `- `, it leaves the variation. A `-`
 * on its own starts an alternative made of the block below it, so Enter there indents as usual.
 */
export function enter(lines: readonly string[], index: number, column: number): EnterAction | undefined {
    if (!isAlternative(lines, index)) {
        return undefined;
    }

    const line = lines[index];
    const indent = indentOf(line);
    if (EmptyAlternative.test(line.slice(indent.length))) {
        return { kind: 'leave', line: indentOf(lines[parentOf(lines, index)!]) };
    }

    const end = trailingId(line)?.contentEnd ?? line.trimEnd().length;
    return line.trim() !== '-' && column >= end ? { kind: 'continue', newLine: `${indent}- ` } : undefined;
}

/**
 * What Tab does on line `index`: a line holding only `- ` becomes a continuation of the alternative above it, indented
 * one level deeper than that alternative. Returns the new line, or `undefined` for VS Code's usual Tab.
 */
export function tab(lines: readonly string[], index: number, unit: string): string | undefined {
    if (!isAlternative(lines, index) || !EmptyAlternative.test(lines[index].trimStart())) {
        return undefined;
    }

    const above = previousAlternative(lines, index);
    return above === undefined ? undefined : indentOf(lines[above]) + unit;
}

/**
 * What happens when line `index` has just become `- ` on a continuation line, one indented under an alternative: it
 * moves out to the alternative's indentation, since a `- ` there would be text. Returns the new line, or `undefined`.
 */
export function dashTyped(lines: readonly string[], index: number): string | undefined {
    if (lines[index].trimStart() !== '- ') {
        return undefined;
    }

    const parent = parentOf(lines, index);
    return parent !== undefined && isAlternative(lines, parent) ? `${indentOf(lines[parent])}- ` : undefined;
}

/** Whether line `index` starts with `- ` or is a lone `-`, directly in a `@sequence` or `@cycle` block. */
function isAlternative(lines: readonly string[], index: number): boolean {
    const parent = parentOf(lines, index);
    return Dash.test(lines[index].trimStart()) && parent !== undefined && Variation.test(lines[parent]);
}

/** The alternative above line `index` in the same block, or `undefined`. */
function previousAlternative(lines: readonly string[], index: number): number | undefined {
    const indent = indentOf(lines[index]).length;
    for (let i = index - 1; i >= 0; i--) {
        if (isBlank(lines[i])) {
            continue;
        }

        const width = indentOf(lines[i]).length;
        if (width < indent) {
            return undefined;
        }

        if (width === indent) {
            return isAlternative(lines, i) ? i : undefined;
        }
    }

    return undefined;
}

/** The nearest line above `index` that's indented less, which is the block it's in, skipping blank and comment lines. */
function parentOf(lines: readonly string[], index: number): number | undefined {
    const indent = indentOf(lines[index]).length;
    for (let i = index - 1; i >= 0; i--) {
        if (!isBlank(lines[i]) && indentOf(lines[i]).length < indent) {
            return i;
        }
    }

    return undefined;
}

function isBlank(line: string): boolean {
    const content = line.trim();
    return content === '' || content.startsWith('//');
}

function indentOf(line: string): string {
    return /^[ \t]*/.exec(line)![0];
}
