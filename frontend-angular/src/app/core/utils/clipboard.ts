/**
 * The Clipboard API is only available in a secure context — HTTPS, localhost or
 * 127.0.0.1 — so this returns false rather than throwing when the page is served
 * over plain HTTP from anything else.
 */
export async function copyText(text: string): Promise<boolean> {
  if (!window.isSecureContext || !navigator.clipboard) return false;

  try {
    await navigator.clipboard.writeText(text);
    return true;
  } catch {
    // Permission denied, or a browser that rejects while the tab is unfocused.
    return false;
  }
}
