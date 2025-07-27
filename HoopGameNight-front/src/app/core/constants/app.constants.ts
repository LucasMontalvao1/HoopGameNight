export const APP_CONSTANTS = {
  HEALTH_CHECK_INTERVAL: 30000, // 30 segundos
  REQUEST_TIMEOUT: 60000,        // 60 segundos

  GAMES_CACHE_DURATION: 300000,  // 5 minutos
  SYNC_CHECK_INTERVAL: 60000,    // 1 minuto
  AUTO_REFRESH_INTERVAL: 120000, // 2 minutos para jogos ao vivo
  
  // Paginação
  DEFAULT_PAGE_SIZE: 25,
  MAX_PAGE_SIZE: 100,

  // Cache Keys
  CACHE_KEYS: {
    TODAY_GAMES: 'today_games',
    GAMES_BY_DATE: 'games_by_date_{date}',
    SYNC_STATUS: 'sync_status'
  }
} as const;