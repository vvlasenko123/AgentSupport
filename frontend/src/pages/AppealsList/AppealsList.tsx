import { useEffect, useMemo, useState } from "react";
import { Link } from "react-router-dom";
import { Search } from "lucide-react";
import { appealsApi } from "../../api/appealsApi";
import type { AppealListItem } from "../../types/appeal";
import "./AppealsList.scss";

function AppealsList() {
  const [appeals, setAppeals] = useState<AppealListItem[]>([]);
  const [search, setSearch] = useState("");
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const fetchAppeals = async () => {
      try {
        setIsLoading(true);
        setError(null);
        const list = await appealsApi.getAppealsList();
        setAppeals(list);
      } catch {
        setError("Не удалось загрузить список обращений. Попробуйте обновить страницу.");
      } finally {
        setIsLoading(false);
      }
    };

    fetchAppeals();
  }, []);

  const filteredAppeals = useMemo(() => {
    const normalized = search.trim().toLowerCase();
    if (!normalized) {
      return appeals;
    }

    return appeals.filter(
      (appeal) =>
        appeal.title.toLowerCase().includes(normalized) ||
        String(appeal.id).includes(normalized),
    );
  }, [appeals, search]);

  return (
    <section className="appeals-list-page">
      <div className="appeals-list-page__head">
        <h1 className="appeals-list-page__title">Обращения</h1>
        <p className="appeals-list-page__subtitle">
          Поиск и просмотр обращений в реестре
        </p>
      </div>

      <div className="appeals-search">
        <Search size={18} />
        <input
          type="text"
          placeholder="Найти по названию или ID..."
          value={search}
          onChange={(event) => setSearch(event.target.value)}
          aria-label="Поиск обращений"
        />
      </div>

      {isLoading && <p className="appeals-list-page__state">Загрузка обращений...</p>}
      {error && <p className="appeals-list-page__state appeals-list-page__state--error">{error}</p>}

      {!isLoading && !error && (
        <ul className="appeals-list">
          {filteredAppeals.length === 0 && (
            <li className="appeals-list__empty">По вашему запросу ничего не найдено.</li>
          )}

          {filteredAppeals.map((appeal, index) => (
            <li
              className="appeals-list__item"
              key={appeal.id}
              style={{ animationDelay: `${index * 0.04}s` }}
            >
              <Link to={`/appeals/${appeal.id}`} className="appeals-card">
                <span className="appeals-card__id">#{appeal.id}</span>
                <h2 className="appeals-card__title">{appeal.title}</h2>
              </Link>
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}

export default AppealsList;
