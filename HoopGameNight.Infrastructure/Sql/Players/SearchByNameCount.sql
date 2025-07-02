﻿SELECT COUNT(*)
FROM players p
WHERE 
    (@Search IS NULL OR @Search = '' OR 
     CONCAT(p.first_name, ' ', p.last_name) LIKE CONCAT('%', @Search, '%') OR
     p.first_name LIKE CONCAT('%', @Search, '%') OR
     p.last_name LIKE CONCAT('%', @Search, '%'))
    AND (@TeamId IS NULL OR p.team_id = @TeamId)
    AND (@Position IS NULL OR @Position = '' OR UPPER(p.position) = UPPER(@Position));