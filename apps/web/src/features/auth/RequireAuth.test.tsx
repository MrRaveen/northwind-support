import { render, screen } from '@testing-library/react';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { describe, expect, it, vi } from 'vitest';
import * as useAuthModule from '../../hooks/useAuth';
import { RequireAuth } from './RequireAuth';

vi.mock('../../hooks/useAuth');

describe('RequireAuth', () => {
    it('redirects unauthenticated visitors to /login preserving the return path', () => {
        vi.spyOn(useAuthModule, 'useAuth').mockReturnValue({
            user: null,
            isAuthenticated: false,
            login: vi.fn(),
            logout: vi.fn(),
        });

        render(
            <MemoryRouter initialEntries={['/tickets/42']}>
                <Routes>
                    <Route
                        path="/tickets/:id"
                        element={
                            <RequireAuth>
                                <div>Protected Ticket Content</div>
                            </RequireAuth>
                        }
                    />
                    <Route path="/login" element={<div>Login Page Screen</div>} />
                </Routes>
            </MemoryRouter>,
        );

        expect(screen.queryByText('Protected Ticket Content')).not.toBeInTheDocument();
        expect(screen.getByText('Login Page Screen')).toBeInTheDocument();
    });

    it('renders children when the agent is authenticated', () => {
        vi.spyOn(useAuthModule, 'useAuth').mockReturnValue({
            user: { id: 1, fullName: 'Alex Turner', email: 'alex@example.com' },
            isAuthenticated: true,
            login: vi.fn(),
            logout: vi.fn(),
        });

        render(
            <MemoryRouter initialEntries={['/tickets/42']}>
                <Routes>
                    <Route
                        path="/tickets/:id"
                        element={
                            <RequireAuth>
                                <div>Protected Ticket Content</div>
                            </RequireAuth>
                        }
                    />
                </Routes>
            </MemoryRouter>,
        );

        expect(screen.getByText('Protected Ticket Content')).toBeInTheDocument();
    });
});