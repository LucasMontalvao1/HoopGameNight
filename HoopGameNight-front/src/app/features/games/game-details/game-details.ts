import { Component, OnInit, OnDestroy, signal, inject, ChangeDetectionStrategy, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { StatsService } from '../../../core/services/stats.service';
import { GamesService } from '../../../core/services/games.service';
import { SEOService } from '../../../core/services/seo.service';
import { SkeletonLoader } from '../../../shared/components/skeleton-loader/skeleton-loader';
import { ErrorView } from '../../../shared/components/error-view/error-view';

@Component({
    selector: 'app-game-details',
    standalone: true,
    imports: [CommonModule, RouterModule, SkeletonLoader, ErrorView],
    templateUrl: './game-details.html',
    styleUrl: './game-details.scss',
    changeDetection: ChangeDetectionStrategy.OnPush
})
export class GameDetails implements OnInit, OnDestroy {
    protected readonly route = inject(ActivatedRoute);
    public readonly router = inject(Router);
    protected readonly statsService = inject(StatsService);
    protected readonly gamesService = inject(GamesService);
    private readonly seoService = inject(SEOService);

    private readonly _gameId = signal<number | null>(null);
    protected readonly activeTab = signal<'boxscore' | 'summary'>('boxscore');

    constructor() { }

    async ngOnInit(): Promise<void> {
        this.route.params.subscribe(async params => {
            const id = params['id'];
            if (id) {
                this._gameId.set(+id);
                await this.loadData(+id);
            }
        });
    }

    ngOnDestroy(): void {
        this.statsService.clearState();
    }

    private async loadData(id: number): Promise<void> {
        await Promise.all([
            this.gamesService.loadGameById(id),
            this.statsService.loadGameBoxscore(id)
        ]);

        const game = this.gamesService.selectedGame();
        if (game) {
            const title = `${game.visitorTeam.displayName} vs ${game.homeTeam.displayName}`;
            this.seoService.updateTitle(title);
            this.seoService.updateMeta(`Boxscore e estatísticas detalhadas: ${title}`);
        }
    }

    setTab(tab: 'boxscore' | 'summary'): void {
        this.activeTab.set(tab);
    }

    goBack(): void {
        this.router.navigate(['/games']);
    }

    getTeamLogo(abbr: string): string {
        return `https://a.espncdn.com/i/teamlogos/nba/500/${abbr.toLowerCase()}.png`;
    }
}
