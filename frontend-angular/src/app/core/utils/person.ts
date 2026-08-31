/**
 * The monogram an account's avatar shows, spelled the same way wherever it appears.
 *
 * Takes a person, so it needs both name parts: the assignee chips render a single letter
 * off a display name instead, which is all those rows carry.
 */
export function initialsOf(person: { firstName?: string; lastName?: string } | null): string {
  if (!person) return '';
  return `${person.firstName?.[0] ?? ''}${person.lastName?.[0] ?? ''}`.toUpperCase();
}

/** The name beside those initials, spelled the same way wherever it is shown. */
export function fullNameOf(person: { firstName?: string; lastName?: string } | null): string {
  if (!person) return '';
  return `${person.firstName ?? ''} ${person.lastName ?? ''}`.trim();
}
