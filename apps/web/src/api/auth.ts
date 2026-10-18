import type { CurrentUser, LoginPayload, LoginResponse } from '../types/api';
import { request } from './client';

export const authApi = {
    login: (payload: LoginPayload) =>
        request<LoginResponse>(
            '/auth/login',
            { method: 'POST', body: JSON.stringify(payload) },
            { anonymous: true },
        ),

    me: () => request<CurrentUser>('/auth/me'),
};