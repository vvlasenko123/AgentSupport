import { useEffect, useMemo, useState } from "react";
import { Link, useParams } from "react-router-dom";
import { ArrowLeft } from "lucide-react";
import { appealsApi } from "../../api/appealsApi";
import type { AppealDetails } from "../../types/appeal";
import "./AppealPage.scss";

const tableRows = (appeal: AppealDetails) => [
  { field: "Дата", value: appeal.date },
  { field: "ФИО", value: appeal.fullName },
  { field: "Объект", value: appeal.objectName },
  { field: "Телефон", value: appeal.phone },
  { field: "Email", value: appeal.email },
  { field: "Заводские номера", value: appeal.serialNumbers },
  { field: "Тип приборов", value: appeal.deviceType },
  { field: "Эмоциональный окрас", value: appeal.emotionalTone },
  { field: "Суть вопроса", value: appeal.issueSummary },
];

function AppealPage() {
  const { id } = useParams<{ id: string }>();
  const appealId = Number(id);

  const [appeal, setAppeal] = useState<AppealDetails | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const fetchAppeal = async () => {
      if (!Number.isFinite(appealId) || appealId <= 0) {
        setError("Некорректный ID обращения.");
        setIsLoading(false);
        return;
      }

      try {
        setIsLoading(true);
        setError(null);
        const details = await appealsApi.getAppealById(appealId);
        setAppeal(details);
      } catch {
        setError("Не удалось получить данные обращения. Проверьте ID и попробуйте снова.");
      } finally {
        setIsLoading(false);
      }
    };

    fetchAppeal();
  }, [appealId]);

  const rows = useMemo(() => (appeal ? tableRows(appeal) : []), [appeal]);

  return (
    <section className="appeal-page">
      <Link to="/appeals" className="appeal-page__back">
        <ArrowLeft size={18} />
        К списку обращений
      </Link>

      <h1 className="appeal-page__title">Карточка обращения #{id}</h1>

      {isLoading && <p className="appeal-page__state">Загрузка данных...</p>}
      {error && <p className="appeal-page__state appeal-page__state--error">{error}</p>}

      {!isLoading && !error && appeal && (
        <div className="appeal-table-wrap">
          <table className="appeal-table">
            <thead>
              <tr>
                <th>Поле</th>
                <th>Значение</th>
              </tr>
            </thead>
            <tbody>
              {rows.map((row) => (
                <tr key={row.field}>
                  <td>{row.field}</td>
                  <td className={row.field === "Суть вопроса" ? "appeal-table__summary" : undefined}>
                    {row.value}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </section>
  );
}

export default AppealPage;
