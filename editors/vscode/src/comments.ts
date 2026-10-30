/**
 * Finds where the comment on a line starts, the index of its `//`, or `undefined` if the line has none. Like the
 * language, it skips a `//` after a backslash, inside a quoted string or in a tag's value. Strings exist only where the
 * language reads code: on `@` lines and headers, in an option's `@` modifiers, and inside `[…]` and `{…}`. In dialogue
 * and option text, a `"` is an ordinary character.
 */
export function commentStart(line: string): number | undefined {
    if (!line.includes('//')) {
        return undefined;
    }

    const option = /^\s*->/.test(line);
    let code = /^\s*(?:@|==|-\s+@)/.test(line);
    let depth = 0;
    for (let i = 0; i < line.length; i++) {
        const c = line[i];
        if (c === '\\') {
            i++;
        } else if (c === '/' && line[i + 1] === '/') {
            return i;
        } else if (c === '"' && (code || depth > 0)) {
            i = stringEnd(line, i);
        } else if (c === '[' || c === '{') {
            depth++;
        } else if ((c === ']' || c === '}') && depth > 0) {
            depth--;
        } else if (c === '#' && startsName(line, i + 1)) {
            i = tagEnd(line, i + 1) - 1;
        } else if (c === '@' && option && startsName(line, i + 1)) {
            code = true;
        }
    }

    return undefined;
}

/** The index of the quote that closes the string opened at `open`, or the line's end if it never closes. */
function stringEnd(line: string, open: number): number {
    for (let i = open + 1; i < line.length; i++) {
        if (line[i] === '\\') {
            i++;
        } else if (line[i] === '"') {
            return i;
        }
    }

    return line.length;
}

/** The index just past the tag whose name starts at `name`: its name ends at whitespace, a `:` or a `//`, and its value runs to whitespace. */
function tagEnd(line: string, name: number): number {
    let i = name;
    while (i < line.length && !/\s/.test(line[i]) && line[i] !== ':' && !line.startsWith('//', i)) {
        i++;
    }

    if (line[i] === ':') {
        while (i < line.length && !/\s/.test(line[i])) {
            i++;
        }
    }

    return i;
}

function startsName(line: string, index: number): boolean {
    return /^\p{L}/u.test(line.slice(index, index + 2));
}
