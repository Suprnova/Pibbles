/**
 * Finds the innermost markup span that's still open at the end of `text`, which is a line's text before a `[/` being
 * typed. A backslash makes the next character literal, as in Pibbles text.
 */
export function openSpan(text: string): string | undefined {
    const open: string[] = [];
    for (let i = 0; i < text.length; i++) {
        if (text[i] === '\\') {
            i++;
        } else if (text[i] === '[') {
            const close = text.startsWith('/', i + 1);
            const name = /^[\p{L}_][\p{L}\p{M}\p{Nd}_]*/u.exec(text.slice(i + (close ? 2 : 1)))?.[0];
            if (name && close) {
                const at = open.lastIndexOf(name);
                if (at >= 0) {
                    open.length = at;
                }
            } else if (name) {
                open.push(name);
            }
        }
    }

    return open.at(-1);
}
