import { useState } from 'react';
import { Navigate, useLocation } from 'react-router-dom';
import { ApiError, toErrorMessage } from '../../api/client';
import { Field } from '../../components/Field';
import { useAuth } from '../../hooks/useAuth';

interface FormErrors {
    email?: string;
    password?: string;
}

function validate(email: string, password: string): FormErrors {
    const errors: FormErrors = {};

    if (email.trim() === '') {
        errors.email = 'Enter your email address.';
    }

    if (password === '') {
        errors.password = 'Enter your password.';
    }

    return errors;
}

/** Only follow in-app paths, never a full URL. */
function safeReturnPath(value: unknown): string {
    return typeof value === 'string' && value.startsWith('/') && !value.startsWith('//')
        ? value
        : '/tickets';
}

export function LoginPage() {
    const { isAuthenticated, login } = useAuth();
    const location = useLocation();
    const returnTo = safeReturnPath((location.state as { from?: string } | null)?.from);

    const [email, setEmail] = useState('');
    const [password, setPassword] = useState('');
    const [errors, setErrors] = useState<FormErrors>({});
    const [submitError, setSubmitError] = useState<string>();
    const [isSaving, setIsSaving] = useState(false);

    // Signed in (just now, or already): go where the user was heading.
    if (isAuthenticated) {
        return <Navigate to={returnTo} replace />;
    }

    const submit = async (event: React.FormEvent<HTMLFormElement>) => {
        event.preventDefault();

        const validationErrors = validate(email, password);
        setErrors(validationErrors);
        setSubmitError(undefined);

        if (Object.keys(validationErrors).length > 0) {
            return;
        }

        setIsSaving(true);

        try {
            await login(email.trim(), password);
        } catch (caught) {
            setSubmitError(
                caught instanceof ApiError && caught.status === 401
                    ? 'The email or password is incorrect.'
                    : toErrorMessage(caught, 'Could not sign in.'),
            );
            setPassword('');
        } finally {
            setIsSaving(false);
        }
    };

    return (
        <main className="login">
            <h1>Northwind Support</h1>
            <p className="page-header__subtitle">Sign in to continue.</p>

            <form className="card form" onSubmit={submit} noValidate>
                <Field id="login-email" label="Email" error={errors.email}>
                    {(fieldProps) => (
                        <input
                            {...fieldProps}
                            type="email"
                            autoComplete="username"
                            value={email}
                            disabled={isSaving}
                            onChange={(event) => setEmail(event.target.value)}
                        />
                    )}
                </Field>

                <Field id="login-password" label="Password" error={errors.password}>
                    {(fieldProps) => (
                        <input
                            {...fieldProps}
                            type="password"
                            autoComplete="current-password"
                            value={password}
                            disabled={isSaving}
                            onChange={(event) => setPassword(event.target.value)}
                        />
                    )}
                </Field>

                {submitError && (
                    <p className="field__error" role="alert">
                        {submitError}
                    </p>
                )}

                <div className="button-row">
                    <button type="submit" className="button button--primary" disabled={isSaving}>
                        {isSaving ? 'Signing in...' : 'Sign in'}
                    </button>
                </div>
            </form>
        </main>
    );
}