/** Where a line's trailing `#id:` tag is: the line's last thing, apart from whitespace. */
export interface TrailingId {
    /** Where the line's content ends, before the whitespace in front of the ID. */
    contentEnd: number;
    /** Where the `#id:` tag starts. */
    idStart: number;
    /** Just past the end of the tag. */
    idEnd: number;
}

const Trailing = /([ \t]+)(#id:[a-z][a-z0-9_]*)[ \t]*$/;

/** Finds the line's trailing `#id:` tag, or `undefined` if the line doesn't end with one. */
export function trailingId(line: string): TrailingId | undefined {
    const match = Trailing.exec(line);
    if (!match) {
        return undefined;
    }

    const idStart = match.index + match[1].length;
    return { contentEnd: match.index, idStart, idEnd: idStart + match[2].length };
}

/**
 * Where **End** should leave a cursor that was at `before` and that VS Code's own End moved to `after`: before the ID,
 * if it moved from the content to past the content's end. Pressed again from there, End goes to the line's real end.
 */
export function endColumn(line: string, before: number, after: number): number {
    const id = trailingId(line);
    return id && before < id.contentEnd && after > id.contentEnd ? id.contentEnd : after;
}

/** Whether **Right** from `column` would step from the content's end into the space before the ID. */
export function stepsIntoId(line: string, column: number): boolean {
    return trailingId(line)?.contentEnd === column;
}

/**
 * Where Enter should break the line, for a cursor at `column`: at the line's end if the cursor is between the content
 * and its trailing ID, so the ID stays on its own line instead of moving to the new one.
 */
export function enterColumn(line: string, column: number): number {
    const id = trailingId(line);
    return id && column >= id.contentEnd && column <= id.idStart ? line.length : column;
}

/**
 * Where a click should leave the cursor: before the ID, if the click landed past the end of the line. A click directly
 * on the ID stays where it is.
 */
export function clickColumn(line: string, column: number): number {
    const id = trailingId(line);
    return id && column >= line.length ? id.contentEnd : column;
}
