import { Component, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { PlayersService } from '../../../core/services/players.service';

@Component({
  selector: 'app-player-details',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './player-details.html',
  styleUrl: './player-details.scss'
})
export class PlayerDetailsComponent implements OnInit, OnDestroy {

  constructor(
    public readonly playersService: PlayersService,
    private readonly route: ActivatedRoute,
    private readonly router: Router
  ) {}

  ngOnInit(): void {
    this.route.params.subscribe(params => {
      const playerId = params['id'];
      if (playerId) {
        this.playersService.loadPlayerById(+playerId);
      }
    });
  }

  ngOnDestroy(): void {
    this.playersService.clearSelectedPlayer();
  }

  goBack(): void {
    this.router.navigate(['/players']);
  }

  viewTeam(abbreviation: string): void {
    this.router.navigate(['/teams', abbreviation]);
  }

  getPlayerPhotoUrl(): string {
    const player = this.playersService.selectedPlayer();
    if (!player) return '';
    return this.playersService.getPlayerPhotoUrl(player.espnId || player.externalId);
  }

  getTeamLogoUrl(): string {
    const player = this.playersService.selectedPlayer();
    if (!player) return '';
    return this.playersService.getTeamLogoUrl(player.team.abbreviation);
  }

  onImageError(event: Event): void {
    const img = event.target as HTMLImageElement;
    img.src = 'assets/player-placeholder.png';
  }

  onTeamLogoError(event: Event): void {
    const img = event.target as HTMLImageElement;
    img.style.display = 'none';
  }
}
