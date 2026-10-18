import { useCallback, useSyncExternalStore } from 'react';
import { authApi } from '../api/auth';
import { clearSession, getSession, saveSession, subscribeToSession } from '../api/session';

/** The signed-in agent, plus login and logout. Re-renders whenever the session changes. */
export function useAuth() {
    const session = useSyncExternalStore(subscribeToSession, getSession);

    const login = useCallback(async (email: string, password: string) => {
        const response = await authApi.login({ email, password });

        saveSession({
            token: response.token,
            expiresAtUtc: response.expiresAtUtc,
            user: response.user,
        });
    }, []);

    const logout = useCallback(() => clearSession(), []);

    return {
        user: session?.user ?? null,
        isAuthenticated: session !== null,
        login,
        logout,
    };
}