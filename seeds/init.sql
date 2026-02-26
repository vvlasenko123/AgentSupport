CREATE EXTENSION IF NOT EXISTS pgcrypto;

CREATE TABLE IF NOT EXISTS complaints
(
    id uuid NOT NULL PRIMARY KEY,
    submission_date timestamp with time zone NOT NULL,
    fio text NOT NULL,
    object_name text NOT NULL,
    phone_number text NULL,
    email text NULL,
    serial_numbers text[] NOT NULL DEFAULT ARRAY[]::text[],
    device_type text NULL,
    emotional_tone text NULL,
    issue_summary text NOT NULL
);

WITH
    ln AS (SELECT v FROM unnest(ARRAY[
                                    'Иванов','Петров','Сидоров','Смирнов','Кузнецов','Попов','Соколов','Лебедев','Козлов','Новиков',
                                'Морозов','Егоров','Волков','Фёдоров','Михайлов','Соловьёв','Павлов','Семенов','Григорьев','Алексеев'
                                    ]) AS v ORDER BY random() LIMIT 1),
    fn AS (SELECT v FROM unnest(ARRAY[
    'Алексей','Иван','Пётр','Сергей','Дмитрий','Андрей','Николай','Михаил','Евгений','Владимир',
    'Анна','Екатерина','Мария','Ольга','Наталья','Ирина','Татьяна','Юлия','Светлана','Елена'
    ]) AS v ORDER BY random() LIMIT 1),
    pn AS (SELECT v FROM unnest(ARRAY[
    'Алексеевич','Иванович','Петрович','Сергеевич','Дмитриевич','Андреевич','Николаевич','Михайлович','Евгеньевич','Владимирович',
    'Алексеевна','Ивановна','Петровна','Сергеевна','Дмитриевна','Андреевна','Николаевна','Михайловна','Евгеньевна','Владимировна'
    ]) AS v ORDER BY random() LIMIT 1),
    cf AS (SELECT v FROM unnest(ARRAY['ООО','АО','ИП']) AS v ORDER BY random() LIMIT 1),
    cn AS (SELECT v FROM unnest(ARRAY[
    'ТехноСфера','ПромСервис','ГородЭнерго','СеверСтрой','ТеплоДом','ИнфоЛайн','ЭлектроПроф','МегаТрейд','СпецМонтаж','Вектор',
    'ПрофИнжиниринг','АльфаСнаб','РегионПоставка','ЦентрСервис','НоваТех'
    ]) AS v ORDER BY random() LIMIT 1),
    dt AS (SELECT v FROM unnest(ARRAY[
    'Счётчик электроэнергии','Теплосчётчик','Контроллер','Датчик давления','Датчик температуры','Модем','Маршрутизатор','ПЛК','Блок питания'
    ]) AS v ORDER BY random() LIMIT 1),
    it AS (SELECT v FROM unnest(ARRAY[
    'Не проходит авторизация в личном кабинете устройства',
    'Устройство не выходит на связь, связь пропадает периодически',
    'После обновления прошивки устройство перестало отправлять данные',
    'Показания не обновляются, данные за последние сутки отсутствуют',
    'Ошибки при запуске, устройство уходит в перезагрузку',
    'Некорректные показания, возможна ошибка измерений',
    'Не удаётся подключиться по сети, требуется проверка настроек',
    'Повышенное время отклика, данные приходят с задержкой',
    'Не работает порт связи, требуется диагностика',
    'Запрос на замену устройства по гарантии'
    ]) AS v ORDER BY random() LIMIT 1),
    tone AS (SELECT v FROM unnest(ARRAY['негативный','нейтральный','позитивный']) AS v ORDER BY random() LIMIT 1)
INSERT INTO complaints
(
    id,
    submission_date,
    fio,
    object_name,
    phone_number,
    email,
    serial_numbers,
    device_type,
    emotional_tone,
    issue_summary
)
SELECT
    gen_random_uuid(),
    NOW() - (random() * interval '10 days'),
    format('%s %s %s', ln.v, fn.v, pn.v),
    format('%s %s', cf.v, cn.v),
    format('+7 %s %s-%s-%s',
           (900 + (random() * 99)::int),
           lpad(((random() * 999)::int)::text, 3, '0'),
           lpad(((random() * 99)::int)::text, 2, '0'),
           lpad(((random() * 99)::int)::text, 2, '0')
    ),
    lower(
            replace(
                    translate(
                            format('%s.%s', fn.v, ln.v),
                            'АБВГДЕЁЖЗИЙКЛМНОПРСТУФХЦЧШЩЪЫЬЭЮЯабвгдеёжзийклмнопрстуфхцчшщъыьэюя',
                            'ABVGDEEZHZIIKLMNOPRSTUFHTSCHSHSH_Y_EYUYAabvgdeezhz_iiklmnoprstufhtschshsh_y_eyuya'
                    ),
                    ' ',
                    ''
            )
    ) || '@mail.ru',
    ARRAY[
        format('SN-2026-%s', lpad(((random() * 999999)::int)::text, 6, '0')),
    format('DEV-%s-%s',
           (ARRAY['MSK','SPB','EKB','KZN','NSK'])[1 + (random() * 4)::int],
           lpad(((random() * 999999)::int)::text, 6, '0')
    )
    ]::text[],
    dt.v,
    tone.v,
    it.v
FROM ln, fn, pn, cf, cn, dt, it, tone;