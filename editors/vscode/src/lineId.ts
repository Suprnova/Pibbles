import { commentStart } from './comments.ts';

/**
 * Where a line's trailing `#id:` tag is, the line's last thing apart from whitespace and a comment, and its ID. A
 * comment after the ID stays visible, so the space before the ID separates the content from it.
 */
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
    /** Where the line's comment starts, at its `//`, or `undefined` if it has none. */
    commentStart: number | undefined;
    /** Where the hidden text starts: the separating space with no comment, and the ID itself before a comment. */
    hiddenStart: number;
    /** Where the hidden text ends: the line's end, or the comment's start. */
    hiddenEnd: number;
}

const Trailing = /[ \t]#id:([a-z][a-z0-9_]*)[ \t]*$/;

/**
 * Finds the line's trailing `#id:` tag, or `undefined` if the line doesn't end with one. Only a comment may follow it,
 * and an ID inside a comment isn't the line's.
 */
export function trailingId(line: string): TrailingId | undefined {
    const comment = commentStart(line);
    const match = Trailing.exec(line.slice(0, comment));
    if (!match) {
        return undefined;
    }

    const idStart = match.index + 1;
    return {
        contentEnd: match.index,
        idStart,
        idEnd: idStart + '#id:'.length + match[1].length,
        value: match[1],
        commentStart: comment,
        hiddenStart: comment === undefined ? match.index : idStart,
        hiddenEnd: comment ?? line.length,
    };
}

/** Whether `value` has a line ID's shape: a lowercase letter, then lowercase letters, digits and `_`. */
export function isLineId(value: string): boolean {
    return /^[a-z][a-z0-9_]*$/.test(value);
}

/**
 * Where a cursor or selection end at `column` goes when the line's ID is hidden, so typing never lands in or after text
 * the writer can't see: a cursor goes no further than the content's end, and a selection end inside the hidden text
 * goes back to it. A selection end at the line's real end stays, so a selection can still take whole lines. With a
 * comment after the ID, a cursor can't be at the comment's `//` either, since text typed there would follow the ID, but
 * a selection end can, so a comment can be selected without the ID.
 */
export function hiddenColumn(line: string, column: number, isCursor: boolean): number {
    const id = trailingId(line);
    const limit = id?.commentStart ?? line.length;
    return id && column > id.contentEnd && (isCursor ? column <= limit : column < limit) ? id.contentEnd : column;
}

/**
 * Where **End** should leave a cursor that was at `before` and that VS Code's own End moved to `after`: before the ID,
 * if it moved from the content to past the content's end. Pressed again from there, End goes to the line's real end.
 * With a comment after the ID, the comment's end is the visible end of the line, so VS Code's own End stands.
 */
export function endColumn(line: string, before: number, after: number): number {
    const id = trailingId(line);
    return id && id.commentStart === undefined && before < id.contentEnd && after > id.contentEnd ? id.contentEnd : after;
}

/** Whether `column` is the content's end of a line whose ID no comment follows, where **Right** and **Delete** would step into the space before the ID. */
export function stepsIntoId(line: string, column: number): boolean {
    const id = trailingId(line);
    return id !== undefined && id.commentStart === undefined && id.contentEnd === column;
}

/**
 * Where **Right** goes from `column`, if it is the content's end of a line whose ID a comment follows: past the
 * comment's `//`, since the cursor can't stop in the hidden ID or before the `//`. **Delete** there does nothing,
 * because the space before the ID is what keeps the ID a tag. `undefined` elsewhere.
 */
export function commentColumn(line: string, column: number): number | undefined {
    const id = trailingId(line);
    return id?.commentStart !== undefined && id.contentEnd === column ? id.commentStart + '//'.length : undefined;
}

/**
 * Where Enter should break the line, for a cursor at `column`: at the line's end if the cursor is between the content
 * and its trailing ID, or its comment, so the ID and the comment stay on their line instead of moving to the new one.
 */
export function enterColumn(line: string, column: number): number {
    const id = trailingId(line);
    return id && column >= id.contentEnd && column <= (id.commentStart ?? id.idStart) ? line.length : column;
}

/**
 * Where a click should leave the cursor: before the ID, if the click landed past the end of the line. A click directly
 * on the ID stays where it is. With a comment after the ID, the end of the line is the comment's end, so it stays.
 */
export function clickColumn(line: string, column: number): number {
    const id = trailingId(line);
    return id && id.commentStart === undefined && column >= line.length ? id.contentEnd : column;
}

/** The line a deletion that joins two lines leaves, and where the cursor goes in it. */
export interface JoinedLine {
    text: string;
    cursor: number;
}

/**
 * Whether a selection within the line from `start` to `end` holds only the hidden ID and the whitespace around it, as
 * a double-click past the end of the line selects. It acts as a cursor then, so the ID can't be selected or dragged.
 * A selection that reaches into a comment after the ID isn't one.
 */
export function selectsOnlyId(line: string, start: number, end: number): boolean {
    const id = trailingId(line);
    return id !== undefined && start >= id.contentEnd && end <= (id.commentStart ?? end);
}

/**
 * What deleting from `start` on the line `first` to `end` on a later line `last` should leave, so each ID stays with its
 * line. If any of `first`'s content is left, the joined line is `first`'s and keeps its ID at the end, and `last`'s ID
 * goes with the deleted text. `first`'s comment stays unless the deletion starts before it, and `last`'s comment
 * follows `first`'s ID unless the deletion reaches into it. Otherwise, the joined line is `last`'s, and only its ID is
 * left. `undefined` if neither line has an ID, or the deletion starts or ends inside an ID, which is a deliberate edit
 * of that ID, or inside a comment's text.
 */
export function joinLines(first: string, start: number, last: string, end: number): JoinedLine | undefined {
    const firstId = trailingId(first);
    const lastId = trailingId(last);
    if (!firstId && !lastId || inside(firstId, start) || inside(lastId, end) || inComment(first, start) || inComment(last, end)) {
        return undefined;
    }

    const head = first.slice(0, Math.min(start, firstId?.contentEnd ?? start));
    if (!head.trim()) {
        return { text: head + last.slice(end), cursor: head.length };
    }

    const lastComment = commentStart(last);
    const lastContentEnd = lastId?.contentEnd ?? (lastComment === undefined ? last.length : last.slice(0, lastComment).trimEnd().length);
    const firstEnd = start > (firstId?.commentStart ?? -1) ? undefined : firstId?.idEnd;
    const kept = lastComment !== undefined && end <= lastComment ? ` ${last.slice(lastComment)}` : '';
    const text = head + last.slice(end, Math.max(end, lastContentEnd)) + (firstId ? first.slice(firstId.contentEnd, firstEnd) : '') + kept;
    return { text, cursor: head.length };
}

function inside(id: TrailingId | undefined, column: number): boolean {
    return id !== undefined && column > id.idStart && column < id.idEnd;
}

/** Whether `column` is in the text of the line's comment, after its `//` and before its end. */
function inComment(line: string, column: number): boolean {
    const start = commentStart(line);
    return start !== undefined && column > start && column < line.length;
}
