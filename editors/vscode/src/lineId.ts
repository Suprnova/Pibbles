/** Where a line's trailing `#id:` tag is, the line's last thing apart from whitespace, and its ID. */
export interface TrailingId {
    /**
     * Where the line's content ends, before the one space or tab that separates it from the ID. Any other whitespace
     * is content, so a space typed at the end of a line counts as text.
     */
    contentEnd: number;
    /** Where the `#id:` tag starts. */
    idStart: number;
    /** Just past the end of the tag. */
    idEnd: number;
    /** The ID itself, without `#id:`. */
    value: string;
}

const Trailing = /[ \t]#id:([a-z][a-z0-9_]*)[ \t]*$/;

/** Finds the line's trailing `#id:` tag, or `undefined` if the line doesn't end with one. */
export function trailingId(line: string): TrailingId | undefined {
    const match = Trailing.exec(line);
    if (!match) {
        return undefined;
    }

    const idStart = match.index + 1;
    return { contentEnd: match.index, idStart, idEnd: idStart + '#id:'.length + match[1].length, value: match[1] };
}

/** Whether `value` has a line ID's shape: a lowercase letter, then lowercase letters, digits and `_`. */
export function isLineId(value: string): boolean {
    return /^[a-z][a-z0-9_]*$/.test(value);
}

/**
 * Where a cursor or selection end at `column` goes when the line's ID is hidden, so typing never lands in or after text
 * the writer can't see: a cursor goes no further than the content's end, and a selection end inside the hidden text
 * goes back to it. A selection end at the line's real end stays, so a selection can still take whole lines.
 */
export function hiddenColumn(line: string, column: number, isCursor: boolean): number {
    const id = trailingId(line);
    return id && column > id.contentEnd && (isCursor || column < line.length) ? id.contentEnd : column;
}

/**
 * Where **End** should leave a cursor that was at `before` and that VS Code's own End moved to `after`: before the ID,
 * if it moved from the content to past the content's end. Pressed again from there, End goes to the line's real end.
 */
export function endColumn(line: string, before: number, after: number): number {
    const id = trailingId(line);
    return id && before < id.contentEnd && after > id.contentEnd ? id.contentEnd : after;
}

/** Whether `column` is the content's end, where **Right** and **Delete** would step into the space before the ID. */
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

/** The line a deletion that joins two lines leaves, and where the cursor goes in it. */
export interface JoinedLine {
    text: string;
    cursor: number;
}

/**
 * Whether a selection within the line that starts at `start` holds only the hidden ID and the whitespace around it, as
 * a double-click past the end of the line selects. It acts as a cursor then, so the ID can't be selected or dragged.
 */
export function selectsOnlyId(line: string, start: number): boolean {
    const id = trailingId(line);
    return id !== undefined && start >= id.contentEnd;
}

/**
 * What deleting from `start` on the line `first` to `end` on a later line `last` should leave, so each ID stays with its
 * line. If any of `first`'s content is left, the joined line is `first`'s and keeps its ID at the end, and `last`'s ID
 * goes with the deleted text. Otherwise, the joined line is `last`'s, and only its ID is left. `undefined` if neither
 * line has an ID, or the deletion starts or ends inside one, which is a deliberate edit of that ID.
 */
export function joinLines(first: string, start: number, last: string, end: number): JoinedLine | undefined {
    const firstId = trailingId(first);
    const lastId = trailingId(last);
    if (!firstId && !lastId || inside(firstId, start) || inside(lastId, end)) {
        return undefined;
    }

    const head = first.slice(0, Math.min(start, firstId?.contentEnd ?? start));
    const text = head.trim()
        ? head + last.slice(end, Math.max(end, lastId?.contentEnd ?? last.length)) + (firstId ? first.slice(firstId.contentEnd) : '')
        : head + last.slice(end);
    return { text, cursor: head.length };
}

function inside(id: TrailingId | undefined, column: number): boolean {
    return id !== undefined && column > id.idStart && column < id.idEnd;
}
