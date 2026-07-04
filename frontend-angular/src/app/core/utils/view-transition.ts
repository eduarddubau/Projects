export interface ViewTransitionHandle {
  ready: Promise<void>;
  finished: Promise<void>;
}

type DocumentWithViewTransition = Document & {
  startViewTransition?: (update: () => void | Promise<void>) => ViewTransitionHandle;
};

/** Starts a view transition, or returns null where the API is unavailable. */
export function startViewTransition(
  doc: Document,
  update: () => void | Promise<void>,
): ViewTransitionHandle | null {
  const document = doc as DocumentWithViewTransition;
  return document.startViewTransition ? document.startViewTransition(update) : null;
}

export function prefersReducedMotion(): boolean {
  return window.matchMedia('(prefers-reduced-motion: reduce)').matches;
}
