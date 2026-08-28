/** Who last touched a record, and when. The key names the phrasing to use. */
export interface Attribution {
  key: 'deletedBy' | 'editedBy' | 'createdBy';
  name: string;
  date: string;
}

/** The audit fields every project and task carries. */
interface Audited {
  createdAt: string;
  createdByDisplayName?: string;
  updatedAt?: string;
  updatedByDisplayName?: string;
  isDeleted?: boolean;
  deletedAt?: string;
}

/** An event nobody can be named for is not worth showing. */
function named(
  key: Attribution['key'],
  name: string | undefined,
  date: string,
): Attribution | null {
  return name ? { key, name, date } : null;
}

/**
 * The most recent thing that happened to a record, and who did it.
 *
 * Two traps. Soft-deleting stamps UpdatedBy/UpdatedAt like any other write, so a trashed
 * record's "last edited by" is really its deletion. And the server sends "" for anyone
 * whose account was soft-deleted — the User query filter hides the row while the FK
 * survives — so an event with no name gives null rather than falling back to an older one,
 * which would read as who last touched it and be wrong.
 */
export function attributionOf(record: Audited): Attribution | null {
  if (record.isDeleted && record.deletedAt) {
    return named('deletedBy', record.updatedByDisplayName, record.deletedAt);
  }

  if (record.updatedAt) {
    return named('editedBy', record.updatedByDisplayName, record.updatedAt);
  }

  return named('createdBy', record.createdByDisplayName, record.createdAt);
}
