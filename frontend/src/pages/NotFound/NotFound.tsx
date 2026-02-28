import { Link } from "react-router-dom";
import "./NotFound.scss";

function NotFound() {
  return (
    <section className="not-found">
      <h1 className="not-found__title">404</h1>
      <p className="not-found__text">Страница не найдена.</p>
      <Link className="not-found__link" to="/home">
        Вернуться на главную
      </Link>
    </section>
  );
}

export default NotFound;
