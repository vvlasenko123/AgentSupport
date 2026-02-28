import { type FormEvent, useEffect, useState } from "react";
import { NavLink } from "react-router-dom";
import { isAuthorized, loginWithPassword, logout } from "../../utils/auth";
import "./Header.scss";

function Header() {
  const [authorized, setAuthorized] = useState<boolean>(() => isAuthorized());
  const [menuOpen, setMenuOpen] = useState(false);

  const [loginOpen, setLoginOpen] = useState(false);
  const [username, setUsername] = useState("");
  const [password, setPassword] = useState("");
  const [authError, setAuthError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  const closeMenu = () => setMenuOpen(false);
  const closeLogin = () => {
    setLoginOpen(false);
    setAuthError(null);
    setPassword("");
  };

  useEffect(() => {
    const syncAuth = () => {
      setAuthorized(isAuthorized());
      if (!isAuthorized()) {
        closeMenu();
      }
    };

    window.addEventListener("agent-auth-changed", syncAuth);
    window.addEventListener("storage", syncAuth);

    return () => {
      window.removeEventListener("agent-auth-changed", syncAuth);
      window.removeEventListener("storage", syncAuth);
    };
  }, []);

  useEffect(() => {
    if (!loginOpen) return;

    const onEsc = (event: KeyboardEvent) => {
      if (event.key === "Escape") {
        closeLogin();
      }
    };

    window.addEventListener("keydown", onEsc);
    return () => window.removeEventListener("keydown", onEsc);
  }, [loginOpen]);

  const handleLoginSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();

    if (!username.trim() || !password.trim()) {
      setAuthError("Введите логин и пароль.");
      return;
    }

    try {
      setIsSubmitting(true);
      setAuthError(null);
      await loginWithPassword({ username: username.trim(), password });
      setAuthorized(true);
      closeLogin();
      closeMenu();
    } catch {
      setAuthError("Ошибка входа. Проверьте логин и пароль.");
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleLogout = () => {
    logout();
    setAuthorized(false);
    closeMenu();
  };

  return (
    <>
      <header className="header">
        <div className="header__container">
          <NavLink className="header__brand" to="/appeals" onClick={closeMenu}>
            Agent support
          </NavLink>

          <div className="header__right">
            <nav className="header__nav">
              <NavLink
                to="/appeals"
                className={({ isActive }) =>
                  isActive ? "header__link header__link--active" : "header__link"
                }
                onClick={closeMenu}
              >
                Обращения
              </NavLink>

              <NavLink
                to="/statistics"
                className={({ isActive }) =>
                  isActive ? "header__link header__link--active" : "header__link"
                }
                onClick={closeMenu}
              >
                Статистика
              </NavLink>
            </nav>

            {!authorized ? (
              <button type="button" className="login-btn" onClick={() => setLoginOpen(true)}>
                Вход
              </button>
            ) : (
              <button type="button" className="login-btn login-btn--ghost" onClick={handleLogout}>
                Выход
              </button>
            )}

            <button
              type="button"
              className={`burger-btn${menuOpen ? " burger-btn--open" : ""}`}
              onClick={() => setMenuOpen((prev) => !prev)}
              aria-label="Меню"
              aria-expanded={menuOpen}
            >
              <span className="burger-line" />
            </button>
          </div>
        </div>

        <div className={`menu-overlay${menuOpen ? " menu-overlay--open" : ""}`} onClick={closeMenu} />

        <div className={`mobile-menu${menuOpen ? " mobile-menu--open" : ""}`}>
          <NavLink
            to="/appeals"
            className={({ isActive }) =>
              isActive ? "mobile-menu-link mobile-menu-link--active" : "mobile-menu-link"
            }
            onClick={closeMenu}
          >
            Обращения
          </NavLink>

          <NavLink
            to="/statistics"
            className={({ isActive }) =>
              isActive ? "mobile-menu-link mobile-menu-link--active" : "mobile-menu-link"
            }
            onClick={closeMenu}
          >
            Статистика
          </NavLink>

          {!authorized ? (
            <button
              type="button"
              className="login-btn login-btn--mobile"
              onClick={() => {
                setLoginOpen(true);
                closeMenu();
              }}
            >
              Вход
            </button>
          ) : (
            <button
              type="button"
              className="login-btn login-btn--ghost login-btn--mobile"
              onClick={handleLogout}
            >
              Выход
            </button>
          )}
        </div>
      </header>

      {loginOpen && (
        <div className="auth-modal" role="dialog" aria-modal="true" aria-labelledby="auth-modal-title">
          <div className="auth-modal__overlay" onClick={closeLogin} />
          <div className="auth-modal__content">
            <h2 id="auth-modal-title" className="auth-modal__title">
              Вход
            </h2>

            <form className="auth-form" onSubmit={handleLoginSubmit}>
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
                <button type="button" className="auth-form__btn auth-form__btn--ghost" onClick={closeLogin}>
                  Отмена
                </button>
                <button type="submit" className="auth-form__btn" disabled={isSubmitting}>
                  {isSubmitting ? "Вход..." : "Войти"}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </>
  );
}

export default Header;
