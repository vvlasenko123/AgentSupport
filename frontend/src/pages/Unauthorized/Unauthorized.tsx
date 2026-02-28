import { Link } from "react-router-dom";
import "./Unauthorized.scss";

function Unauthorized() {
  return (
    <section className="unauthorized">
      <h1 className="unauthorized__title">401</h1>
      <p className="unauthorized__text">Доступ к этой странице ограничен.</p>
      <Link className="unauthorized__link" to="/home">
        Вернуться на главную
      </Link>
    </section>
  );
}

export default Unauthorized;
