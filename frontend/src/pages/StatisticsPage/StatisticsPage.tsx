import { useEffect, useMemo, useState } from "react";
import {
  Bar,
  BarChart,
  CartesianGrid,
  Cell,
  Legend,
  Line,
  LineChart,
  Pie,
  PieChart,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from "recharts";
import { appealsApi } from "../../utils/appealsApi";
import type { ComplaintDto } from "../../types/appeal";
import "./StatisticsPage.scss";

type ChartItem = { name: string; value: number };

type DailyItem = {
  day: string;
  count: number;
};

const PIE_COLORS = ["#2e96ea", "#5ab4f5", "#1873bb", "#8ecbff", "#1b4f87", "#4d8fc9"];

const getDateKey = (date: Date) =>
  `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, "0")}-${String(
    date.getDate(),
  ).padStart(2, "0")}`;

const toLabel = (date: Date) =>
  `${String(date.getDate()).padStart(2, "0")}.${String(date.getMonth() + 1).padStart(2, "0")}`;

const normalize = (value?: string) => {
  const safe = value?.trim();
  return safe && safe.length > 0 ? safe : "Не указано";
};

const truncate = (value: string, max: number) => (value.length > max ? `${value.slice(0, max)}...` : value);

const buildDistribution = (items: ComplaintDto[], getField: (item: ComplaintDto) => string): ChartItem[] => {
  const map = new Map<string, number>();

  items.forEach((item) => {
    const key = normalize(getField(item));
    map.set(key, (map.get(key) ?? 0) + 1);
  });

  return Array.from(map.entries())
    .map(([name, value]) => ({ name, value }))
    .sort((a, b) => b.value - a.value);
};

function StatisticsPage() {
  const [isMobile, setIsMobile] = useState(() => window.innerWidth <= 600);
  const [complaints, setComplaints] = useState<ComplaintDto[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const onResize = () => setIsMobile(window.innerWidth <= 600);
    window.addEventListener("resize", onResize);
    return () => window.removeEventListener("resize", onResize);
  }, []);

  useEffect(() => {
    const fetchData = async () => {
      try {
        setIsLoading(true);
        setError(null);
        const data = await appealsApi.getComplaintsRaw();
        setComplaints(data);
      } catch {
        setError("Не удалось загрузить статистику. Попробуйте обновить страницу.");
      } finally {
        setIsLoading(false);
      }
    };

    fetchData();
  }, []);

  const stats = useMemo(() => {
    const total = complaints.length;

    const uniqueObjects = new Set(complaints.map((item) => normalize(item.objectName))).size;

    const avgSerialCount =
      total === 0
        ? 0
        : complaints.reduce((sum, item) => sum + (item.serialNumbers?.length ?? 0), 0) / total;

    const positiveShare =
      total === 0
        ? 0
        :
            (complaints.filter((item) => item.emotionalTone.toLowerCase().includes("позит")).length / total) *
            100;

    const statusesData = buildDistribution(complaints, (item) => item.status);
    const tonesData = buildDistribution(complaints, (item) => item.emotionalTone);
    const deviceData = buildDistribution(complaints, (item) => item.deviceType).slice(0, 6);

    const now = new Date();
    const byDateMap = new Map<string, number>();

    complaints.forEach((item) => {
      const date = new Date(item.submissionDate);
      if (Number.isNaN(date.getTime())) return;
      const key = getDateKey(date);
      byDateMap.set(key, (byDateMap.get(key) ?? 0) + 1);
    });

    const dailyData: DailyItem[] = Array.from({ length: 7 }, (_, index) => {
      const dayDate = new Date(now);
      dayDate.setDate(now.getDate() - (6 - index));
      const key = getDateKey(dayDate);
      return {
        day: toLabel(dayDate),
        count: byDateMap.get(key) ?? 0,
      };
    });

    return {
      total,
      uniqueObjects,
      avgSerialCount,
      positiveShare,
      statusesData,
      tonesData,
      deviceData,
      dailyData,
    };
  }, [complaints]);

  return (
    <section className="statistics-page">
      <div className="appeals-list-page__head">
        <h1 className="appeals-list-page__title">Статистика</h1>
        <p className="appeals-list-page__subtitle">Аналитика и обзор ключевых показателей по обращениям</p>
      </div>

      {isLoading && <p className="statistics-page__state">Загрузка аналитики...</p>}
      {error && <p className="statistics-page__state statistics-page__state--error">{error}</p>}

      {!isLoading && !error && (
        <>
          <div className="kpi-grid">
            <article className="kpi-card">
              <p className="kpi-card__label">Всего обращений</p>
              <p className="kpi-card__value">{stats.total}</p>
            </article>
            <article className="kpi-card">
              <p className="kpi-card__label">Уникальных объектов</p>
              <p className="kpi-card__value">{stats.uniqueObjects}</p>
            </article>
            <article className="kpi-card">
              <p className="kpi-card__label">Среднее кол-во серийных номеров</p>
              <p className="kpi-card__value">{stats.avgSerialCount.toFixed(1)}</p>
            </article>
            <article className="kpi-card">
              <p className="kpi-card__label">Позитивная тональность</p>
              <p className="kpi-card__value">{stats.positiveShare.toFixed(0)}%</p>
            </article>
          </div>

          <div className="statistics-grid">
            <article className="chart-card">
              <h2 className="chart-card__title">Динамика обращений (7 дней)</h2>
              <div className="chart-card__canvas">
                <ResponsiveContainer width="100%" height="100%">
                  <LineChart
                    data={stats.dailyData}
                    margin={{ top: 10, right: isMobile ? 6 : 18, left: isMobile ? -24 : -14, bottom: 0 }}
                  >
                    <CartesianGrid strokeDasharray="3 3" stroke="#d5e8f9" />
                    <XAxis dataKey="day" stroke="#4d7ca5" fontSize={isMobile ? 10 : 12} />
                    <YAxis allowDecimals={false} stroke="#4d7ca5" fontSize={isMobile ? 10 : 12} />
                    <Tooltip />
                    <Line
                      type="monotone"
                      dataKey="count"
                      name="Обращения"
                      stroke="#2e96ea"
                      strokeWidth={3}
                      dot={{ r: 4 }}
                    />
                  </LineChart>
                </ResponsiveContainer>
              </div>
            </article>

            <article className="chart-card">
              <h2 className="chart-card__title">Распределение по статусам</h2>
              <div className="chart-card__canvas">
                <ResponsiveContainer width="100%" height="100%">
                  <BarChart
                    data={stats.statusesData}
                    margin={{ top: 10, right: isMobile ? 4 : 16, left: isMobile ? -10 : 6, bottom: 10 }}
                  >
                    <CartesianGrid strokeDasharray="3 3" stroke="#d5e8f9" />
                    <XAxis
                      dataKey="name"
                      stroke="#4d7ca5"
                      fontSize={isMobile ? 10 : 12}
                      interval={0}
                      angle={isMobile ? 0 : -10}
                      height={isMobile ? 34 : 44}
                      tickFormatter={(value) => truncate(value, isMobile ? 10 : 14)}
                    />
                    <YAxis allowDecimals={false} stroke="#4d7ca5" fontSize={isMobile ? 10 : 12} />
                    <Tooltip />
                    <Bar dataKey="value" name="Количество" radius={[8, 8, 0, 0]}>
                      {stats.statusesData.map((entry, index) => (
                        <Cell key={entry.name} fill={PIE_COLORS[index % PIE_COLORS.length]} />
                      ))}
                    </Bar>
                  </BarChart>
                </ResponsiveContainer>
              </div>
            </article>

            <article className="chart-card">
              <h2 className="chart-card__title">Тональность обращений</h2>
              <div className="chart-card__canvas">
                <ResponsiveContainer width="100%" height="100%">
                  <PieChart>
                    <Pie
                      data={stats.tonesData}
                      dataKey="value"
                      nameKey="name"
                      cx="50%"
                      cy="50%"
                      outerRadius={isMobile ? 76 : 92}
                      innerRadius={isMobile ? 38 : 45}
                      paddingAngle={2}
                    >
                      {stats.tonesData.map((entry, index) => (
                        <Cell key={entry.name} fill={PIE_COLORS[index % PIE_COLORS.length]} />
                      ))}
                    </Pie>
                    <Tooltip />
                    {!isMobile && <Legend />}
                  </PieChart>
                </ResponsiveContainer>
              </div>
            </article>

            <article className="chart-card">
              <h2 className="chart-card__title">Топ типов устройств</h2>
              <div className="chart-card__canvas">
                <ResponsiveContainer width="100%" height="100%">
                  <BarChart
                    data={stats.deviceData}
                    layout="vertical"
                    margin={{ top: 10, right: isMobile ? 6 : 14, left: isMobile ? 8 : 28, bottom: 10 }}
                  >
                    <CartesianGrid strokeDasharray="3 3" stroke="#d5e8f9" />
                    <XAxis type="number" allowDecimals={false} stroke="#4d7ca5" fontSize={isMobile ? 10 : 12} />
                    <YAxis
                      type="category"
                      dataKey="name"
                      stroke="#4d7ca5"
                      fontSize={isMobile ? 10 : 12}
                      width={isMobile ? 72 : 110}
                      tickFormatter={(value) => truncate(value, isMobile ? 10 : 18)}
                    />
                    <Tooltip />
                    <Bar dataKey="value" name="Количество" radius={[0, 8, 8, 0]} fill="#2e96ea" />
                  </BarChart>
                </ResponsiveContainer>
              </div>
            </article>
          </div>
        </>
      )}
    </section>
  );
}

export default StatisticsPage;
