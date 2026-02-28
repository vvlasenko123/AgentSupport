import { useEffect, useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import AuthModal from "../../components/AuthModal/AuthModal";
import { isAuthorized } from "../../utils/auth";
import "./HomePage.scss";

function HomePage() {
  const navigate = useNavigate();
  const [authorized, setAuthorized] = useState<boolean>(() => isAuthorized());
  const [loginOpen, setLoginOpen] = useState(false);

  useEffect(() => {
    const syncAuth = () => setAuthorized(isAuthorized());
    window.addEventListener("agent-auth-changed", syncAuth);
    window.addEventListener("storage", syncAuth);

    return () => {
      window.removeEventListener("agent-auth-changed", syncAuth);
      window.removeEventListener("storage", syncAuth);
    };
  }, []);

  return (
    <>
      <section className="home-page">
        <div className="home-page__hero">
          <p className="home-page__eyebrow">Agent support</p>
          <h1 className="home-page__title">Система обработки обращений</h1>
          <p className="home-page__subtitle">
            Войдите в систему, чтобы просматривать обращения, анализировать статистику и работать с
            карточками заявок в едином интерфейсе.
          </p>

          <div className="home-page__actions">
            {authorized ? (
              <Link to="/appeals" className="home-page__btn">
                Перейти к обращениям
              </Link>
            ) : (
              <button type="button" className="home-page__btn" onClick={() => setLoginOpen(true)}>
                Войти в систему
              </button>
            )}
          </div>
        </div>
      </section>

      <AuthModal
        open={loginOpen}
        onClose={() => setLoginOpen(false)}
        onSuccess={() => {
          setAuthorized(true);
          navigate("/appeals", { replace: true });
        }}
      />
    </>
  );
}

export default HomePage;
