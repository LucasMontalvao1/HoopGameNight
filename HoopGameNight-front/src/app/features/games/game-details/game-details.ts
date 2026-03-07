import { Component, OnInit, OnDestroy, signal, inject, ChangeDetectionStrategy, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { StatsService } from '../../../core/services/stats.service';
import { GamesService } from '../../../core/services/games.service';
import { AskApiService } from '../../../core/services/ask-api.service';
import { SEOService } from '../../../core/services/seo.service';
import { SkeletonLoader } from '../../../shared/components/skeleton-loader/skeleton-loader';
import { ErrorView } from '../../../shared/components/error-view/error-view';
import { SafeHtmlPipe } from '../../../shared/pipes/safe-html.pipe';
import { marked } from 'marked';

@Component({
    selector: 'app-game-details',
    standalone: true,
    imports: [CommonModule, RouterModule, SkeletonLoader, ErrorView, SafeHtmlPipe],
    templateUrl: './game-details.html',
    styleUrl: './game-details.scss',
    changeDetection: ChangeDetectionStrategy.OnPush
})
export class GameDetails implements OnInit, OnDestroy {
    protected readonly route = inject(ActivatedRoute);
    public readonly router = inject(Router);
    protected readonly statsService = inject(StatsService);
    protected readonly gamesService = inject(GamesService);
    private readonly askService = inject(AskApiService);
    private readonly seoService = inject(SEOService);

    private readonly _gameId = signal<number | null>(null);
    protected readonly activeTab = signal<'boxscore' | 'summary'>('boxscore');
    protected readonly gameSummary = signal<string | null>(null);
    protected readonly isLoadingSummary = signal<boolean>(false);

    protected readonly gameSummaryHtml = computed(() => {
        const summary = this.gameSummary();
        // marcado v4+ pode retornar Promise se não configurado. Forçando síncrono.
        return summary ? (marked.parse(summary, { async: false }) as string) : '';
    });

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

    async setTab(tab: 'boxscore' | 'summary'): Promise<void> {
        this.activeTab.set(tab);

        if (tab === 'summary' && !this.gameSummary()) {
            await this.loadGameSummary();
        }
    }

    private async loadGameSummary(): Promise<void> {
        const id = this._gameId();
        if (!id) return;

        this.isLoadingSummary.set(true);
        try {
            const response = await this.askService.getGameSummary(id);
            this.gameSummary.set(response.answer);
        } catch (error) {
            console.error('Erro ao carregar resumo IA:', error);
            this.gameSummary.set('Não foi possível gerar o resumo automático para este jogo no momento.');
        } finally {
            this.isLoadingSummary.set(false);
        }
    }

    goBack(): void {
        this.router.navigate(['/games']);
    }

    getTeamLogo(abbr: string): string {
        return `https://a.espncdn.com/i/teamlogos/nba/500/${abbr.toLowerCase()}.png`;
    }
}
