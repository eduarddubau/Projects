import { attributionOf } from './attribution';

const created = {
  createdAt: '2026-01-01T10:00:00Z',
  createdByDisplayName: 'Ana Pop',
};

describe('attributionOf', () => {
  it('names the creator when nothing has been edited', () => {
    expect(attributionOf(created)).toEqual({
      key: 'createdBy',
      name: 'Ana Pop',
      date: '2026-01-01T10:00:00Z',
    });
  });

  it('prefers the last editor once there is one', () => {
    expect(
      attributionOf({
        ...created,
        updatedAt: '2026-02-02T10:00:00Z',
        updatedByDisplayName: 'Bogdan Ilie',
      }),
    ).toEqual({ key: 'editedBy', name: 'Bogdan Ilie', date: '2026-02-02T10:00:00Z' });
  });

  // The server sends "" for an author whose account was soft-deleted — the User query
  // filter hides the row while the FK survives. Falling back to the creator here would
  // read as "who last touched this" and name the wrong person, so it says nothing instead.
  it('says nothing when the last editor has no name left', () => {
    expect(
      attributionOf({ ...created, updatedAt: '2026-02-02T10:00:00Z', updatedByDisplayName: '' }),
    ).toBeNull();
  });

  it('gives nothing when no name survives at all', () => {
    expect(
      attributionOf({ createdAt: '2026-01-01T10:00:00Z', createdByDisplayName: '' }),
    ).toBeNull();
  });

  // An edit timestamp with no name at all is the same case.
  it('says nothing when an edit carries no name', () => {
    expect(attributionOf({ ...created, updatedAt: '2026-02-02T10:00:00Z' })).toBeNull();
  });

  // Soft-deleting stamps UpdatedBy/UpdatedAt like any other write, so without this a
  // trashed project reported its own deletion as "last edited by" — an edit that never
  // happened, dated to the deletion.
  it('calls a deletion a deletion rather than an edit', () => {
    expect(
      attributionOf({
        ...created,
        updatedAt: '2026-03-03T10:00:00Z',
        updatedByDisplayName: 'Bogdan Ilie',
        isDeleted: true,
        deletedAt: '2026-03-03T10:00:00Z',
      }),
    ).toEqual({ key: 'deletedBy', name: 'Bogdan Ilie', date: '2026-03-03T10:00:00Z' });
  });

  it('says nothing when the deleter has no name left', () => {
    expect(
      attributionOf({ ...created, isDeleted: true, deletedAt: '2026-03-03T10:00:00Z' }),
    ).toBeNull();
  });
});
