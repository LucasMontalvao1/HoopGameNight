export interface HealthCheckResponse {
  status: string; 
  timestamp: string;
  duration: number;
  checks: HealthCheckItem[];
  summary: HealthCheckSummary;
}

export interface HealthCheckItem {
  name: string;
  status: string; 
  description: string | null;
  duration: number;
  tags: string[];
  data?: Record<string, any>;
  error?: string | null;
}

export interface HealthCheckSummary {
  healthy: number;
  degraded: number;
  unhealthy: number;
  total: number;
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
    currentPage: number;
    page?: number;
    pageSize: number;
    totalCount: number;
    totalPages: number;
    hasNext?: boolean;
    hasPrevious?: boolean;
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
  displayName: string;
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

// Player Interfaces
export interface PlayerResponse {
  id: number;
  externalId: number;
  nbaStatsId?: string | null;
  espnId?: string | null;
  firstName: string;
  lastName: string;
  fullName: string;
  position: string;
  positionDisplay: string;
  height: string;
  weight: string;
  team: TeamSummaryResponse;
  displayName: string;
}

export interface SearchPlayerRequest {
  search?: string;
  teamId?: number;
  position?: string;
  page?: number;
  pageSize?: number;
}

export enum PlayerPosition {
  PG = 'PG',
  SG = 'SG',
  SF = 'SF',
  PF = 'PF',
  C = 'C'
}

export const POSITION_NAMES: Record<PlayerPosition, string> = {
  [PlayerPosition.PG]: 'Point Guard',
  [PlayerPosition.SG]: 'Shooting Guard',
  [PlayerPosition.SF]: 'Small Forward',
  [PlayerPosition.PF]: 'Power Forward',
  [PlayerPosition.C]: 'Center'
};
