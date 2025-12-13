import { Injectable, signal, computed } from '@angular/core';
import { TeamsApiService } from './teams-api.service';
import { StorageService } from './storage.service';
import { TeamResponse, Division } from '../interfaces/api.interface';

@Injectable({
  providedIn: 'root'
})
export class TeamsService {
  private readonly _allTeams = signal<TeamResponse[]>([]);
  private readonly _selectedTeam = signal<TeamResponse | null>(null);
  private readonly _isLoading = signal<boolean>(false);
  private readonly _error = signal<string | null>(null);
  private readonly _lastUpdate = signal<Date | null>(null);

  readonly allTeams = this._allTeams.asReadonly();
  readonly selectedTeam = this._selectedTeam.asReadonly();
  readonly isLoading = this._isLoading.asReadonly();
  readonly error = this._error.asReadonly();
  readonly lastUpdate = this._lastUpdate.asReadonly();

  readonly easternConference = computed(() => 
    this._allTeams().filter(team => team.conference === 'East')
  );

  readonly westernConference = computed(() => 
    this._allTeams().filter(team => team.conference === 'West')
  );

  readonly teamsByDivision = computed(() => {
    const teams = this._allTeams();
    const grouped = teams.reduce((acc, team) => {
      if (!acc[team.division]) {
        acc[team.division] = [];
      }
      acc[team.division].push(team);
      return acc;
    }, {} as Record<string, TeamResponse[]>);

    Object.keys(grouped).forEach(division => {
      grouped[division].sort((a, b) => {
        const aName = a.displayName || `${a.city} ${a.name}`;
        const bName = b.displayName || `${b.city} ${b.name}`;
        return aName.localeCompare(bName);
      });
    });

    return grouped;
  });

  readonly divisions = computed(() => 
    Array.from(new Set(this._allTeams().map(team => team.division))).sort()
  );

  private readonly NBA_TEAMS = new Set([
    'ATL', 'BOS', 'BKN', 'CHA', 'CHI', 'CLE', 'DAL', 'DEN', 'DET', 'GS', 'GWS',
    'HOU', 'IND', 'LAC', 'LAL', 'MEM', 'MIA', 'MIL', 'MIN', 'NO', 'NY',
    'OKC', 'ORL', 'PHI', 'PHX', 'POR', 'SAC', 'SA', 'TOR', 'UTAH', 'WSH'
  ]);

  constructor(
    private readonly teamsApiService: TeamsApiService,
    private readonly storageService: StorageService
  ) {
    this.loadFromCache();
  }

  async loadAllTeams(forceRefresh = false): Promise<void> {
    if (!forceRefresh && this._allTeams().length > 0) {
      console.log('Teams já carregados, usando cache');
      return;
    }

    this._isLoading.set(true);
    this._error.set(null);

    try {
      console.log('Carregando teams da API...');
      const allTeams = await this.teamsApiService.getAllTeams();
      
      const nbaTeams = this.filterNBATeamsOnly(allTeams);
      
      this._allTeams.set(nbaTeams);
      this._lastUpdate.set(new Date());
      
      await this.saveToCache();
      
      console.log(`${nbaTeams.length} times NBA carregados com sucesso (${allTeams.length - nbaTeams.length} times não-NBA filtrados)`);
    } catch (error) {
      const errorMessage = `Erro ao carregar teams: ${error}`;
      console.error(errorMessage);
      this._error.set(errorMessage);
    } finally {
      this._isLoading.set(false);
    }
  }

  private filterNBATeamsOnly(teams: TeamResponse[]): TeamResponse[] {
    return teams.filter(team => {
      const upperAbbr = team.abbreviation.toUpperCase();
      const isValidNBA = this.NBA_TEAMS.has(upperAbbr);
      
      if (!isValidNBA) {
        console.log(`Time não-NBA filtrado: ${team.abbreviation} - ${team.displayName || team.name}`);
      }
      
      return isValidNBA;
    });
  }

  isNBATeam(abbreviation: string): boolean {
    return this.NBA_TEAMS.has(abbreviation.toUpperCase());
  }

  async loadTeamById(teamId: number): Promise<TeamResponse | null> {
    this._isLoading.set(true);
    this._error.set(null);

    try {
      console.log(`Carregando team ${teamId}...`);
      const team = await this.teamsApiService.getTeamById(teamId);
      
      if (team && this.isNBATeam(team.abbreviation)) {
        this._selectedTeam.set(team);
        const teamName = team.displayName || `${team.city} ${team.name}`;
        console.log(`Team ${teamName} carregado`);
        return team;
      } else if (team) {
        console.log(`Time não-NBA rejeitado: ${team.abbreviation}`);
        return null;
      }
      
      return null;
    } catch (error) {
      const errorMessage = `Erro ao carregar team: ${error}`;
      console.error(errorMessage);
      this._error.set(errorMessage);
      return null;
    } finally {
      this._isLoading.set(false);
    }
  }

  searchTeams(query: string): TeamResponse[] {
    if (!query.trim()) return this._allTeams();

    const searchTerm = query.toLowerCase().trim();
    return this._allTeams().filter(team => {
      const displayName = team.displayName || `${team.city} ${team.name}`;
      return (
        displayName.toLowerCase().includes(searchTerm) ||
        team.abbreviation.toLowerCase().includes(searchTerm) ||
        team.city.toLowerCase().includes(searchTerm) ||
        team.name.toLowerCase().includes(searchTerm)
      );
    });
  }

  getTeamsByConference(conference: 'East' | 'West'): TeamResponse[] {
    return this._allTeams().filter(team => team.conference === conference);
  }

  getTeamsByDivision(division: string): TeamResponse[] {
    return this._allTeams().filter(team => team.division === division);
  }

  getTeamByAbbreviation(abbreviation: string): TeamResponse | undefined {
    return this._allTeams().find(team => 
      team.abbreviation.toLowerCase() === abbreviation.toLowerCase()
    );
  }

  getTeamLogoUrl(abbreviation: string): string {
    if (!this.isNBATeam(abbreviation)) {
      return 'data:image/svg+xml;base64,PHN2ZyB4bWxucz0iaHR0cDovL3d3dy53My5vcmcvMjAwMC9zdmciIHZpZXdCb3g9IjAgMCAxMDAgMTAwIiB3aWR0aD0iMTAwIiBoZWlnaHQ9IjEwMCI+CiAgPHJlY3Qgd2lkdGg9IjEwMCIgaGVpZ2h0PSIxMDAiIHJ4PSIxMCIgZmlsbD0iI2YzZjRmNiIvPgogIDx0ZXh0IHg9IjUwIiB5PSI1MCIgdGV4dC1hbmNob3I9Im1pZGRsZSIgZG9taW5hbnQtYmFzZWxpbmU9Im1pZGRsZSIgZm9udC1mYW1pbHk9IkFyaWFsLCBzYW5zLXNlcmlmIiBmb250LXNpemU9IjE0IiBmaWxsPSIjOWNhM2FmIj5OQkE8L3RleHQ+Cjwvc3ZnPg==';
    }

    // Mapeamento correto de abreviações para ESPN CDN
    const logoMappings: Record<string, string> = {
      'ATL': 'atl',   // Atlanta Hawks
      'BOS': 'bos',   // Boston Celtics
      'BKN': 'bkn',   // Brooklyn Nets
      'CHA': 'cha',   // Charlotte Hornets
      'CHI': 'chi',   // Chicago Bulls
      'CLE': 'cle',   // Cleveland Cavaliers
      'DAL': 'dal',   // Dallas Mavericks
      'DEN': 'den',   // Denver Nuggets
      'DET': 'det',   // Detroit Pistons
      'GS': 'gs',     // Golden State Warriors (banco usa GS)
      'GSW': 'gs',    // Golden State Warriors (fallback)
      'HOU': 'hou',   // Houston Rockets
      'IND': 'ind',   // Indiana Pacers
      'LAC': 'lac',   // LA Clippers
      'LAL': 'lal',   // LA Lakers
      'MEM': 'mem',   // Memphis Grizzlies
      'MIA': 'mia',   // Miami Heat
      'MIL': 'mil',   // Milwaukee Bucks
      'MIN': 'min',   // Minnesota Timberwolves
      'NOP': 'no',    // New Orleans Pelicans
      'NO': 'no',     // New Orleans Pelicans (banco usa NO)
      'NY': 'ny',     // New York Knicks (banco usa NY)
      'NYK': 'ny',    // New York Knicks (fallback)
      'OKC': 'okc',   // Oklahoma City Thunder
      'ORL': 'orl',   // Orlando Magic
      'PHI': 'phi',   // Philadelphia 76ers
      'PHX': 'phx',   // Phoenix Suns
      'POR': 'por',   // Portland Trail Blazers
      'SAC': 'sac',   // Sacramento Kings
      'SA': 'sa',     // San Antonio Spurs (banco usa SA)
      'SAS': 'sa',    // San Antonio Spurs (fallback)
      'TOR': 'tor',   // Toronto Raptors
      'UTAH': 'utah', // Utah Jazz (banco usa UTAH)
      'UTA': 'utah',  // Utah Jazz (fallback)
      'WSH': 'wsh',   // Washington Wizards (banco usa WSH)
      'WAS': 'wsh'    // Washington Wizards (fallback)
    };

    const upperAbbr = abbreviation.toUpperCase();
    const logoAbbr = logoMappings[upperAbbr] || abbreviation.toLowerCase();

    return `https://a.espncdn.com/i/teamlogos/nba/500/${logoAbbr}.png`;
  }

  getTeamColorPrimary(abbreviation: string): string {
    const teamColors: Record<string, string> = {
      'LAL': '#552583', 'GS': '#1D428A', 'GSW': '#1D428A', 'BOS': '#007A33',
      'MIA': '#98002E', 'CHI': '#CE1141', 'NY': '#006BB6', 'NYK': '#006BB6',
      'LAC': '#C8102E', 'BKN': '#000000', 'PHI': '#006BB6',
      'MIL': '#00471B', 'PHX': '#E56020', 'UTAH': '#002B5C', 'UTA': '#002B5C',
      'ATL': '#E03A3E', 'DEN': '#0E2240', 'IND': '#002D62',
      'CLE': '#860038', 'MEM': '#5D76A9', 'DAL': '#00538C',
      'TOR': '#CE1141', 'CHA': '#1D1160', 'SA': '#C4CED4', 'SAS': '#C4CED4',
      'MIN': '#0C2340', 'OKC': '#007AC1', 'NOP': '#0C2340', 'NO': '#0C2340',
      'ORL': '#0077C0', 'WSH': '#002B5C', 'WAS': '#002B5C', 'SAC': '#5A2D81',
      'DET': '#C8102E', 'HOU': '#CE1141', 'POR': '#E03A3E'
    };

    return teamColors[abbreviation.toUpperCase()] || '#6B7280';
  }

  getTeamColorSecondary(abbreviation: string): string {
    const teamColors: Record<string, string> = {
      'LAL': '#FDB927', 'GS': '#FFC72C', 'GSW': '#FFC72C', 'BOS': '#BA9653',
      'MIA': '#F9A01B', 'CHI': '#000000', 'NY': '#FF6900', 'NYK': '#FF6900',
      'LAC': '#1D428A', 'BKN': '#FFFFFF', 'PHI': '#ED174C',
      'MIL': '#EEE1C6', 'PHX': '#1D1160', 'UTAH': '#F9A01B', 'UTA': '#F9A01B',
      'ATL': '#C1D32F', 'DEN': '#FEC524', 'IND': '#FDBB30',
      'CLE': '#FDBB30', 'MEM': '#12173F', 'DAL': '#00538C',
      'TOR': '#000000', 'CHA': '#00788C', 'SA': '#000000', 'SAS': '#000000',
      'MIN': '#78BE20', 'OKC': '#EF3B24', 'NOP': '#85714D', 'NO': '#85714D',
      'ORL': '#C4CED4', 'WSH': '#E31837', 'WAS': '#E31837', 'SAC': '#63727A',
      'DET': '#006BB6', 'HOU': '#000000', 'POR': '#000000'
    };

    return teamColors[abbreviation.toUpperCase()] || '#E5E7EB';
  }

  async syncTeams(): Promise<void> {
    console.log('Sincronizando teams...');
    
    this._isLoading.set(true);
    this._error.set(null);

    try {
      await this.teamsApiService.syncTeams();
      await this.loadAllTeams(true);
      console.log('Teams sincronizados com sucesso');
    } catch (error) {
      const errorMessage = `Erro ao sincronizar teams: ${error}`;
      console.error(errorMessage);
      this._error.set(errorMessage);
    } finally {
      this._isLoading.set(false);
    }
  }

  private async loadFromCache(): Promise<void> {
    try {
      const cachedTeams = await this.storageService.getAppData<TeamResponse[]>('teams', 'all');
      const cachedUpdate = await this.storageService.getAppData<string>('teams', 'last_update');
      
      if (cachedTeams && cachedTeams.length > 0) {
        const nbaTeams = this.filterNBATeamsOnly(cachedTeams);
        this._allTeams.set(nbaTeams);
        
        if (cachedUpdate) {
          this._lastUpdate.set(new Date(cachedUpdate));
        }
        
        console.log(`${nbaTeams.length} times NBA carregados do cache (${cachedTeams.length - nbaTeams.length} times não-NBA filtrados)`);
        
        if (cachedTeams.length !== nbaTeams.length) {
          await this.saveToCache();
        }
      }
    } catch (error) {
      console.warn('Erro ao carregar teams do cache:', error);
    }
  }

  private async saveToCache(): Promise<void> {
    try {
      await this.storageService.setAppData('teams', 'all', this._allTeams());
      await this.storageService.setAppData('teams', 'last_update', new Date().toISOString());
      console.log('Teams salvos no cache');
    } catch (error) {
      console.warn('Erro ao salvar teams no cache:', error);
    }
  }

  clearCache(): void {
    this._allTeams.set([]);
    this._selectedTeam.set(null);
    this._lastUpdate.set(null);
    this._error.set(null);
    
    this.storageService.clearAppData('teams');
    
    console.log('Cache de teams limpo');
  }

  getStats(): { totalTeams: number, conferences: number, divisions: number, lastUpdate: Date | null } {
    return {
      totalTeams: this._allTeams().length,
      conferences: 2,
      divisions: this.divisions().length,
      lastUpdate: this._lastUpdate()
    };
  }
}