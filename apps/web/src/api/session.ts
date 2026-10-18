import type { CurrentUser } from '../types/api';

const STORAGE_KEY = 'supportdesk.session';

export interface Session {
    token: string;
    expiresAtUtc: string;
    user: CurrentUser;
}

const listeners = new Set<() => void>();

// undefined = not read from storage yet. The same object is returned until the session changes,
// which useSyncExternalStore needs.
let current: Session | null | undefined;

function readFromStorage(): Session | null {
    try {
        const raw = window.localStorage.getItem(STORAGE_KEY);

        if (!raw) {
            return null;
        }

        const session = JSON.parse(raw) as Session;

        if (new Date(session.expiresAtUtc).getTime() <= Date.now()) {
            window.localStorage.removeItem(STORAGE_KEY);
            return null;
        }

        return session;
    } catch {
        return null;
    }
}

function notify() {
    listeners.forEach((listener) => listener());
}

export function getSession(): Session | null {
    if (current === undefined) {
        current = readFromStorage();
    }

    return current;
}

export function saveSession(session: Session) {
    window.localStorage.setItem(STORAGE_KEY, JSON.stringify(session));
    current = session;
    notify();
}

export function clearSession() {
    window.localStorage.removeItem(STORAGE_KEY);
    current = null;
    notify();
}

export function subscribeToSession(listener: () => void): () => void {
    listeners.add(listener);

    return () => {
        listeners.delete(listener);
    };
}

// Signing out (or in) in another tab updates this one.
if (typeof window !== 'undefined') {
    window.addEventListener('storage', (event) => {
        if (event.key === STORAGE_KEY) {
            current = readFromStorage();
            notify();
        }
    });
}