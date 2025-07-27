export interface HealthCheckResponse {
  status: 'healthy' | 'unhealthy';
  uptime: number;
  timestamp: string;
  version?: string;
  environment?: string;
  server?: string;
}

export enum ApiStatus {
  ONLINE = 'online',
  OFFLINE = 'offline',
  LOADING = 'loading',
  ERROR = 'error'
}

export interface ApiResponse<T> {
  success: boolean;
  message: string;
  data: T;
  timestamp: string;
}

export interface PaginatedResponse<T> {
  success: boolean;
  message: string;
  data: T[];
  pagination: {
    page: number;
    pageSize: number;
    totalCount: number;
    totalPages: number;
  };
  timestamp: string;
}

export interface GameResponse {
  id: number;
  date: string;
  dateTime: string;
  homeTeam: TeamSummaryResponse;
  visitorTeam: TeamSummaryResponse;
  homeTeamScore?: number;
  visitorTeamScore?: number;
  status: string;
  statusDisplay: string;
  period?: number;
  timeRemaining?: string;
  postSeason: boolean;
  season: number;
  score: string;
  gameTitle: string;
  isLive: boolean;
  isCompleted: boolean;
  winningTeam?: TeamSummaryResponse;
}

export interface TeamSummaryResponse {
  id: number;
  name: string;
  abbreviation: string;
  city: string;
  logoUrl?: string;
}

export interface GetGamesRequest {
  page?: number;
  pageSize?: number;
  date?: string;
  startDate?: string;
  endDate?: string;
  teamId?: number;
  status?: GameStatus;
  postSeason?: boolean;
  season?: number;
}

export interface SyncStatusResponse {
  localGames: number;
  externalGames: number;
  needsSync: boolean;
  lastCheck: string;
  recommendation: string;
}

export enum GameStatus {
  SCHEDULED = 'Scheduled',
  LIVE = 'Live', 
  FINAL = 'Final',
  POSTPONED = 'Postponed',
  CANCELLED = 'Cancelled'
}

export interface TeamResponse {
  id: number;
  abbreviation: string;
  city: string;
  conference: 'East' | 'West';
  division: string;
  fullName: string;
  name: string;
  displayName?: string; 
}

export interface GetTeamsRequest {
  page?: number;
  pageSize?: number;
  conference?: 'East' | 'West';
  division?: string;
  search?: string;
}

export interface Division {
  name: string;
  teams: TeamResponse[];
}