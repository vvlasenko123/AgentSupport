import { NavLink } from "react-router-dom";
import "./Header.scss";

function Header() {
  return (
    <header className="header">
      <div className="header__container">
        <NavLink className="header__brand" to="/appeals">
          Agent support
        </NavLink>
        <nav className="header__nav">
          <NavLink
            to="/appeals"
            className={({ isActive }) =>
              isActive ? "header__link header__link--active" : "header__link"
            }
          >
            Обращения
          </NavLink>
        </nav>
      </div>
    </header>
  );
}

export default Header;
