import { useEffect, useMemo, useState } from "react";
import { Link } from "react-router-dom";
import { Search } from "lucide-react";
import { appealsApi } from "../../api/appealsApi";
import type { AppealListItem, SortOrder } from "../../types/appeal";
import "./AppealsList.scss";

function AppealsList() {
  const [appeals, setAppeals] = useState<AppealListItem[]>([]);
  const [search, setSearch] = useState("");
  const [sortOrder, setSortOrder] = useState<SortOrder>("default");

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

  const filteredAndSortedAppeals = useMemo(() => {
    let result = [...appeals];

    const normalized = search.trim().toLowerCase();
    if (normalized) {
      result = result.filter(
        (appeal) =>
          appeal.title.toLowerCase().includes(normalized) ||
          String(appeal.id).includes(normalized),
      );
    }

    if (sortOrder !== "default") {
      result.sort((a, b) => {
        const parseDate = (dateStr: string) => {
          const [day, month, year] = dateStr.split(".").map(Number);
          return new Date(year, month - 1, day).getTime();
        };

        const dateA = parseDate(a.date);
        const dateB = parseDate(b.date);

        return sortOrder === "desc" ? dateB - dateA : dateA - dateB;
      });
    }

    return result;
  }, [appeals, search, sortOrder]);

  const toggleSort = () => {
    setSortOrder((prev) => {
      if (prev === "default") return "desc";
      if (prev === "desc") return "asc";
      return "default";
    });
  };

  return (
    <section className="appeals-list-page">
      <div className="appeals-list-page__head">
        <h1 className="appeals-list-page__title">Обращения</h1>
        <p className="appeals-list-page__subtitle">Поиск и просмотр обращений в реестре</p>
      </div>

      <div className="appeals-search">
        <Search size={18} />
        <input
          type="text"
          placeholder="Найти по названию или ID..."
          value={search}
          onChange={(event) => setSearch(event.target.value)}
        />
      </div>

      {isLoading && <p className="appeals-list-page__state">Загрузка обращений...</p>}
      {error && <p className="appeals-list-page__state appeals-list-page__state--error">{error}</p>}

      {!isLoading && !error && (
        <div className="appeals-table">
          <div className="appeals-table__header">
            <span className="col-title">Номер</span>
            <span className="col-title">Обращение</span>
            <span className="col-date sortable" onClick={toggleSort} role="button" tabIndex={0}>
              Дата {sortOrder === "desc" ? "↓" : sortOrder === "asc" ? "↑" : ""}
            </span>
          </div>

          <ul className="appeals-list">
            {filteredAndSortedAppeals.length === 0 && (
              <li className="appeals-list__empty">По вашему запросу ничего не найдено.</li>
            )}

            {filteredAndSortedAppeals.map((appeal, index) => (
              <li
                className="appeals-list__item"
                key={appeal.id}
                style={{ animationDelay: `${index * 0.03}s` }}
              >
                <Link to={`/appeals/${appeal.id}`} className="appeals-card">
                  <span className="appeals-card__id">#{appeal.id}</span>
                  <span className="appeals-card__title">{appeal.title}</span>
                  <span className="appeals-card__date">{appeal.date}</span>
                </Link>
              </li>
            ))}
          </ul>
        </div>
      )}
    </section>
  );
}

export default AppealsList;
