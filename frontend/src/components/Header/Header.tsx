import { useEffect, useState } from "react";
import { NavLink } from "react-router-dom";
import { loginWithTelegram } from "../../utils/auth";
import type { TelegramAuthPayload } from "../../utils/auth";
import "./Header.scss";

const TG_BOT_USERNAME = import.meta.env.VITE_TG_BOT_USERNAME || "data_pointbot";

declare global {
  interface Window {
    onTelegramAuth?: (user: TelegramAuthPayload) => void;
  }
}

function Header() {
  const [authorized, setAuthorized] = useState<boolean>(() => !!localStorage.getItem("access_token"));
  const [menuOpen, setMenuOpen] = useState(false);

  const closeMenu = () => setMenuOpen(false);

  useEffect(() => {
    const syncAuth = () => {
      setAuthorized(!!localStorage.getItem("access_token"));
      if (!localStorage.getItem("access_token")) closeMenu();
    };

    window.addEventListener("agent-auth-changed", syncAuth);
    window.addEventListener("storage", syncAuth);

    return () => {
      window.removeEventListener("agent-auth-changed", syncAuth);
      window.removeEventListener("storage", syncAuth);
    };
  }, []);

  useEffect(() => {
    window.onTelegramAuth = async (tgUser: TelegramAuthPayload) => {
      try {
        await loginWithTelegram({
          id: tgUser.id,
          first_name: tgUser.first_name,
          last_name: tgUser.last_name,
          username: tgUser.username,
          photo_url: tgUser.photo_url,
          auth_date: Number(tgUser.auth_date),
          hash: tgUser.hash,
        });
        setAuthorized(true);
        closeMenu();
      } catch (error) {
        console.error("Telegram login failed", error);
      }
    };

    return () => {
      delete window.onTelegramAuth;
    };
  }, []);

  useEffect(() => {
    if (authorized) return;

    const hosts = document.querySelectorAll(".tg-login-host");
    hosts.forEach((host) => {
      host.innerHTML = "";
      const script = document.createElement("script");
      script.src = "https://telegram.org/js/telegram-widget.js?22";
      script.async = true;
      script.setAttribute("data-telegram-login", TG_BOT_USERNAME);
      script.setAttribute("data-size", "large");
      script.setAttribute("data-userpic", "false");
      script.setAttribute("data-request-access", "write");
      script.setAttribute("data-lang", "ru");
      script.setAttribute("data-onauth", "onTelegramAuth(user)");
      host.appendChild(script);
    });
  }, [authorized]);

  return (
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

          {!authorized && <div className="tg-login-wrapper tg-login-host" />}

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

        {!authorized && <div className="tg-login-wrapper tg-login-host tg-login-wrapper--mobile" />}
      </div>
    </header>
  );
}

export default Header;
