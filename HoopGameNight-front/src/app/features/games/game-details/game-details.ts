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
    protected readonly gameSummaryMarkdown = signal<string | null>(null);
    protected readonly gameSummaryHighlights = signal<any[]>([]);
    protected readonly isLoadingSummary = signal<boolean>(false);

    protected readonly sortField = signal<string | null>(null);
    protected readonly sortDirection = signal<'asc' | 'desc'>('desc');

    protected readonly visitorTotals = computed(() => this.statsService.visitorTeamTotals());
    protected readonly homeTotals = computed(() => this.statsService.homeTeamTotals());

    protected readonly gameSummaryHtml = computed(() => {
        const summary = this.gameSummaryMarkdown();
        return summary ? (marked.parse(summary, { async: false }) as string) : '';
    });

    protected readonly sortedVisitorStats = computed(() => {
        return this.sortStats(this.statsService.currentGameBoxscore()?.visitorTeamStats || []);
    });

    protected readonly sortedHomeStats = computed(() => {
        return this.sortStats(this.statsService.currentGameBoxscore()?.homeTeamStats || []);
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
        if (tab === 'summary' && !this.gameSummaryMarkdown()) {
            await this.loadGameSummary();
        }
    }

    private async loadGameSummary(): Promise<void> {
        const id = this._gameId();
        if (!id) return;

        this.isLoadingSummary.set(true);
        try {
            const response = await this.askService.getGameSummary(id);
            // Backend retorna JSON: {summary: "markdown...", highlights: [...]}
            try {
                const parsed = JSON.parse(response.answer);
                this.gameSummaryMarkdown.set(parsed.summary ?? response.answer);
                this.gameSummaryHighlights.set(parsed.highlights ?? []);
            } catch {
                // Fallback: resposta já é texto puro
                this.gameSummaryMarkdown.set(response.answer);
                this.gameSummaryHighlights.set([]);
            }
        } catch (error) {
            console.error('Erro ao carregar resumo IA:', error);
            this.gameSummaryMarkdown.set('Não foi possível gerar o resumo automático para este jogo no momento.');
        } finally {
            this.isLoadingSummary.set(false);
        }
    }

    goBack(): void {
        this.router.navigate(['/games']);
    }

    toggleSort(field: string): void {
        if (this.sortField() === field) {
            this.sortDirection.set(this.sortDirection() === 'asc' ? 'desc' : 'asc');
        } else {
            this.sortField.set(field);
            this.sortDirection.set('desc');
        }
    }

    private sortStats(stats: any[]): any[] {
        const field = this.sortField();
        const direction = this.sortDirection();
        if (!field) return stats;
        return [...stats].sort((a, b) => {
            const valA = a[field] ?? 0;
            const valB = b[field] ?? 0;
            return direction === 'asc' ? valA - valB : valB - valA;
        });
    }

    getTeamLogo(abbr: string): string {
        return `https://a.espncdn.com/i/teamlogos/nba/500/${abbr.toLowerCase()}.png`;
    }

    getFgPct(totals: { fieldGoalsMade?: number; fieldGoalsAttempted?: number } | undefined | null): string {
        if (!totals || !totals.fieldGoalsAttempted) return '—';
        return ((totals.fieldGoalsMade! / totals.fieldGoalsAttempted) * 100).toFixed(1);
    }
}
