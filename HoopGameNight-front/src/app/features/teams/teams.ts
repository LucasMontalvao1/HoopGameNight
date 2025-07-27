import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { FormsModule } from '@angular/forms';

import { TeamsService } from '../../core/services/teams.service';
import { TeamResponse } from '../../core/interfaces/api.interface';

@Component({
  selector: 'app-teams',
  standalone: true,
  imports: [CommonModule, RouterModule, FormsModule],
  templateUrl: './teams.html',
  styleUrls: ['./teams.scss']
})
export class Teams implements OnInit {
  private readonly _searchQuery = signal<string>('');
  private readonly _selectedConference = signal<'All' | 'East' | 'West'>('All');
  private readonly _selectedDivision = signal<string>('All');
  private readonly _viewMode = signal<'grid' | 'list' | 'divisions'>('grid');
  private readonly _sortBy = signal<'name' | 'conference' | 'division'>('name');

  readonly searchQuery = this._searchQuery.asReadonly();
  readonly selectedConference = this._selectedConference.asReadonly();
  readonly selectedDivision = this._selectedDivision.asReadonly();
  readonly viewMode = this._viewMode.asReadonly();
  readonly sortBy = this._sortBy.asReadonly();

  constructor(protected readonly teamsService: TeamsService) {}

  async ngOnInit(): Promise<void> {
    console.log('Teams component inicializado');
    await this.loadInitialData();
  }


  onSearchInput(event: Event): void {
    const target = event.target as HTMLInputElement;
    this._searchQuery.set(target.value);
  }

  onSearchChange(query: string): void {
    this._searchQuery.set(query);
  }

  onConferenceSelect(event: Event): void {
    const target = event.target as HTMLSelectElement;
    const value = target.value as 'All' | 'East' | 'West';
    this.onConferenceChange(value);
  }

  onConferenceChange(conference: 'All' | 'East' | 'West'): void {
    this._selectedConference.set(conference);
    if (conference !== 'All') {
      this._selectedDivision.set('All'); 
    }
  }

  onDivisionSelect(event: Event): void {
    const target = event.target as HTMLSelectElement;
    this.onDivisionChange(target.value);
  }

  onDivisionChange(division: string): void {
    this._selectedDivision.set(division);
  }

  onSortSelect(event: Event): void {
    const target = event.target as HTMLSelectElement;
    const value = target.value as 'name' | 'conference' | 'division';
    this.onSortChange(value);
  }

  onSortChange(sortBy: 'name' | 'conference' | 'division'): void {
    this._sortBy.set(sortBy);
  }

  onViewModeChange(mode: 'grid' | 'list' | 'divisions'): void {
    this._viewMode.set(mode);
  }

  clearFilters(): void {
    this._searchQuery.set('');
    this._selectedConference.set('All');
    this._selectedDivision.set('All');
    this._sortBy.set('name');
  }

  getFilteredTeams(): TeamResponse[] {
    let teams = this.teamsService.allTeams();

    if (this._searchQuery()) {
      teams = this.teamsService.searchTeams(this._searchQuery());
    }

    if (this._selectedConference() !== 'All') {
      teams = teams.filter(team => team.conference === this._selectedConference());
    }

    if (this._selectedDivision() !== 'All') {
      teams = teams.filter(team => team.division === this._selectedDivision());
    }

    return this.sortTeams(teams);
  }

  private sortTeams(teams: TeamResponse[]): TeamResponse[] {
    const sortBy = this._sortBy();
    
    return [...teams].sort((a, b) => {
      switch (sortBy) {
        case 'name':
          const aName = a.displayName || `${a.city} ${a.name}`;
          const bName = b.displayName || `${b.city} ${b.name}`;
          return aName.localeCompare(bName);
        case 'conference':
          if (a.conference !== b.conference) {
            return a.conference.localeCompare(b.conference);
          }
          const aNameConf = a.displayName || `${a.city} ${a.name}`;
          const bNameConf = b.displayName || `${b.city} ${b.name}`;
          return aNameConf.localeCompare(bNameConf);
        case 'division':
          if (a.division !== b.division) {
            return a.division.localeCompare(b.division);
          }
          const aNameDiv = a.displayName || `${a.city} ${a.name}`;
          const bNameDiv = b.displayName || `${b.city} ${b.name}`;
          return aNameDiv.localeCompare(bNameDiv);
        default:
          return 0;
      }
    });
  }

  getTeamsByDivision(): Record<string, TeamResponse[]> {
    const filteredTeams = this.getFilteredTeams();
    const grouped = filteredTeams.reduce((acc, team) => {
      if (!acc[team.division]) {
        acc[team.division] = [];
      }
      acc[team.division].push(team);
      return acc;
    }, {} as Record<string, TeamResponse[]>);

    return grouped;
  }

  getTeamsByDivisionArray(): Array<{key: string, value: TeamResponse[]}> {
    const divisions = this.getTeamsByDivision();
    return Object.entries(divisions).map(([key, value]) => ({ key, value }));
  }

  getTeamDisplayName(team: TeamResponse): string {
    return team.displayName || `${team.city} ${team.name}`;
  }

  handleLogoError(event: Event, abbreviation: string): void {
    const img = event.target as HTMLImageElement;
    
    if (img.src.includes('nba-logo-fallback.svg')) {
      return;
    }
    
    console.warn(`Logo não encontrado para: ${abbreviation}`);
    
    img.src = 'assets/images/nba-logo-fallback.svg';
    
    img.classList.add('logo-fallback');
  }


  async syncTeams(): Promise<void> {
    await this.teamsService.syncTeams();
  }

  async refreshTeams(): Promise<void> {
    await this.teamsService.loadAllTeams(true);
  }

  async selectTeam(team: TeamResponse): Promise<void> {
    await this.teamsService.loadTeamById(team.id);
    const teamName = team.displayName || `${team.city} ${team.name}`;
    console.log('Team selected:', teamName);
    // abrir um modal
  }

  getTeamLogoUrl(abbreviation: string): string {
    return this.teamsService.getTeamLogoUrl(abbreviation);
  }

  getTeamColorPrimary(abbreviation: string): string {
    return this.teamsService.getTeamColorPrimary(abbreviation);
  }

  getTeamColorSecondary(abbreviation: string): string {
    return this.teamsService.getTeamColorSecondary(abbreviation);
  }

  trackByTeamId(index: number, team: TeamResponse): number {
    return team.id;
  }

  trackByDivisionEntry(index: number, item: {key: string, value: TeamResponse[]}): string {
    return item.key;
  }

  getConferenceIcon(conference: string): string {
    return conference === 'East' ? 'east' : 'west';
  }

  getDivisionTeamsCount(division: string): number {
    return this.teamsService.getTeamsByDivision(division).length;
  }

  getFilterSummary(): string {
    const total = this.getFilteredTeams().length;
    const allTeams = this.teamsService.allTeams().length;
    
    if (total === allTeams) {
      return `${total} times`;
    }
    
    return `${total} de ${allTeams} times`;
  }

  get easternConferenceTeams(): TeamResponse[] {
    const filtered = this.getFilteredTeams();
    return filtered.filter(team => team.conference === 'East');
  }

  get westernConferenceTeams(): TeamResponse[] {
    const filtered = this.getFilteredTeams();
    return filtered.filter(team => team.conference === 'West');
  }

  get availableDivisions(): string[] {
    const teams = this.teamsService.allTeams();
    const conference = this._selectedConference();
    
    if (conference === 'All') {
      return this.teamsService.divisions();
    }
    
    const conferenceTeams = teams.filter(team => team.conference === conference);
    const divisions = new Set(conferenceTeams.map(team => team.division));
    return Array.from(divisions).sort();
  }

  get hasActiveFilters(): boolean {
    return (
      this._searchQuery() !== '' ||
      this._selectedConference() !== 'All' ||
      this._selectedDivision() !== 'All'
    );
  }

  private async loadInitialData(): Promise<void> {
    try {
      await this.teamsService.loadAllTeams();
      console.log('Teams data loaded successfully');
    } catch (error) {
      console.error('Error loading teams data:', error);
    }
  }
}