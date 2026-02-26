DELETE FROM complaints
WHERE id IN
      (
          SELECT id
          FROM complaints
          ORDER BY submission_date DESC
    LIMIT 1
    );