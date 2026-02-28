import { type FormEvent, useEffect, useState } from "react";
import { loginWithPassword } from "../../utils/auth";
import "./AuthModal.scss";

type AuthModalProps = {
  open: boolean;
  onClose: () => void;
  onSuccess?: () => void;
};

function AuthModal({ open, onClose, onSuccess }: AuthModalProps) {
  const [username, setUsername] = useState("");
  const [password, setPassword] = useState("");
  const [authError, setAuthError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  useEffect(() => {
    if (!open) return;

    const onEsc = (event: KeyboardEvent) => {
      if (event.key === "Escape") onClose();
    };

    window.addEventListener("keydown", onEsc);
    return () => window.removeEventListener("keydown", onEsc);
  }, [open, onClose]);

  useEffect(() => {
    if (open) return;
    setPassword("");
    setAuthError(null);
  }, [open]);

  const handleSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();

    if (!username.trim() || !password.trim()) {
      setAuthError("Введите логин и пароль.");
      return;
    }

    try {
      setIsSubmitting(true);
      setAuthError(null);
      await loginWithPassword({ username: username.trim(), password });
      onClose();
      onSuccess?.();
    } catch {
      setAuthError("Ошибка входа. Проверьте логин и пароль.");
    } finally {
      setIsSubmitting(false);
    }
  };

  if (!open) return null;

  return (
    <div className="auth-modal" role="dialog" aria-modal="true" aria-labelledby="auth-modal-title">
      <div className="auth-modal__overlay" onClick={onClose} />
      <div className="auth-modal__content">
        <h2 id="auth-modal-title" className="auth-modal__title">
          Вход
        </h2>

        <form className="auth-form" onSubmit={handleSubmit}>
          <label className="auth-form__label">
            Логин
            <input
              className="auth-form__input"
              type="text"
              autoComplete="username"
              value={username}
              onChange={(event) => setUsername(event.target.value)}
            />
          </label>

          <label className="auth-form__label">
            Пароль
            <input
              className="auth-form__input"
              type="password"
              autoComplete="current-password"
              value={password}
              onChange={(event) => setPassword(event.target.value)}
            />
          </label>

          {authError && <p className="auth-form__error">{authError}</p>}

          <div className="auth-form__actions">
            <button type="button" className="auth-form__btn auth-form__btn--ghost" onClick={onClose}>
              Отмена
            </button>
            <button type="submit" className="auth-form__btn" disabled={isSubmitting}>
              {isSubmitting ? "Вход..." : "Войти"}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}

export default AuthModal;
