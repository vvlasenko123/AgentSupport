import { useEffect, useState } from "react";
import { NavLink, useNavigate } from "react-router-dom";
import AuthModal from "../AuthModal/AuthModal";
import { isAuthorized, logout } from "../../utils/auth";
import "./Header.scss";

function Header() {
  const navigate = useNavigate();
  const [authorized, setAuthorized] = useState<boolean>(() => isAuthorized());
  const [menuOpen, setMenuOpen] = useState(false);
  const [loginOpen, setLoginOpen] = useState(false);

  const closeMenu = () => setMenuOpen(false);
  const closeLogin = () => setLoginOpen(false);

  useEffect(() => {
    const syncAuth = () => {
      const auth = isAuthorized();
      setAuthorized(auth);

      if (!auth) {
        closeMenu();
        closeLogin();
      }
    };

    window.addEventListener("agent-auth-changed", syncAuth);
    window.addEventListener("storage", syncAuth);

    return () => {
      window.removeEventListener("agent-auth-changed", syncAuth);
      window.removeEventListener("storage", syncAuth);
    };
  }, []);

  const handleLogout = () => {
    logout();
    setAuthorized(false);
    closeMenu();
    navigate("/home", { replace: true });
  };

  return (
    <>
      <header className="header">
        <div className="header__container">
          <NavLink className="header__brand" to="/home" onClick={closeMenu}>
            Agent support
          </NavLink>

          <div className="header__right">
            <nav className="header__nav">
              <NavLink
                to="/home"
                className={({ isActive }) =>
                  isActive ? "header__link header__link--active" : "header__link"
                }
                onClick={closeMenu}
              >
                Главная
              </NavLink>

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
            to="/home"
            className={({ isActive }) =>
              isActive ? "mobile-menu-link mobile-menu-link--active" : "mobile-menu-link"
            }
            onClick={closeMenu}
          >
            Главная
          </NavLink>

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

      <AuthModal
        open={loginOpen}
        onClose={closeLogin}
        onSuccess={() => {
          setAuthorized(true);
          closeMenu();
          navigate("/appeals", { replace: true });
        }}
      />
    </>
  );
}

export default Header;
