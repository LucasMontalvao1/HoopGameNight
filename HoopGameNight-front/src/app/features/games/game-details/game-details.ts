import { Component, OnInit, OnDestroy, signal, inject, ChangeDetectionStrategy, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { StatsService } from '../../../core/services/stats.service';
import { GamesService } from '../../../core/services/games.service';
import { AskApiService } from '../../../core/services/ask-api.service';
import { SEOService } from '../../../core/services/seo.service';
import { SkeletonLoader } from '../../../shared/components/skeleton-loader/skeleton-loader';
import { ErrorView } from '../../../shared/components/error-view/error-view';
import { marked } from 'marked';

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
    private readonly askService = inject(AskApiService);
    private readonly seoService = inject(SEOService);

    private readonly _gameId = signal<number | null>(null);
    protected readonly activeTab = signal<'summary' | 'boxscore' | 'playbyplay' | 'h2h'>('summary');
    protected readonly gameSummaryMarkdown = computed(() => this.statsService.currentGameSummary());
    protected readonly gameSummaryHighlights = computed(() => this.statsService.currentGameHighlights());
    protected readonly isLoadingSummary = signal<boolean>(false);

    protected readonly sortField = signal<string | null>(null);
    protected readonly sortDirection = signal<'asc' | 'desc'>('desc');

    protected readonly boxscoreTeamView = signal<'visitor' | 'home'>('visitor');

    protected readonly visitorTotals = computed(() => this.statsService.visitorTeamTotals());
    protected readonly homeTotals = computed(() => this.statsService.homeTeamTotals());

    protected readonly gameSummaryHtml = computed(() => {
        const summary = this.gameSummaryMarkdown();
        return summary ? (marked.parse(summary, { async: false }) as string) : '';
    });

    protected readonly activeBoxscoreStats = computed(() => {
        const stats = this.boxscoreTeamView() === 'visitor' 
            ? this.statsService.currentGameBoxscore()?.visitorTeamStats 
            : this.statsService.currentGameBoxscore()?.homeTeamStats;
        return this.sortStats(stats || []);
    });

    protected readonly selectedTeamName = computed(() => {
        const game = this.gamesService.selectedGame();
        if (!game) return '';
        return this.boxscoreTeamView() === 'visitor' 
            ? game.visitorTeam.displayName 
            : game.homeTeam.displayName;
    });

    protected readonly topLeaders = computed(() => {
        const leaders = this.gamesService.gameLeaders();
        if (!leaders) return null;

        const getTop = (cat: 'pointsLeader' | 'reboundsLeader' | 'assistsLeader') => {
            const visitorVal = leaders.visitorTeamLeaders?.[cat]?.value || 0;
            const homeVal = leaders.homeTeamLeaders?.[cat]?.value || 0;
            return visitorVal >= homeVal ? leaders.visitorTeamLeaders?.[cat] : leaders.homeTeamLeaders?.[cat];
        };

        return {
            pts: getTop('pointsLeader'),
            reb: getTop('reboundsLeader'),
            ast: getTop('assistsLeader')
        };
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
        // Manter o estado para persistência ao voltar para a mesma tela
        // this.statsService.clearState();
    }

    private async loadData(id: number): Promise<void> {
        await Promise.all([
            this.gamesService.loadGameById(id),
            this.statsService.loadGameBoxscore(id),
            this.gamesService.loadGameLeaders(id)
        ]);

        const game = this.gamesService.selectedGame();
        if (game) {
            const title = `${game.visitorTeam.displayName} vs ${game.homeTeam.displayName}`;
            this.seoService.updateTitle(title);
            this.seoService.updateMeta(`Boxscore e estatísticas detalhadas: ${title}`);
        }

        // Auto-load resumo se for a aba ativa e ainda não tiver dado
        if (this.activeTab() === 'summary' && !this.statsService.currentGameSummary()) {
            this.loadGameSummary();
        }
    }

    async setTab(tab: 'summary' | 'boxscore' | 'playbyplay' | 'h2h'): Promise<void> {
        this.activeTab.set(tab);
        if (tab === 'summary' && !this.statsService.currentGameSummary()) {
            await this.loadGameSummary();
        } else if (tab === 'playbyplay') {
            await this.gamesService.loadGamePlays(this._gameId()!);
        } else if (tab === 'h2h') {
            await this.gamesService.loadHeadToHead(this._gameId()!);
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
                this.statsService.setGameSummary(
                    parsed.summary ?? response.answer,
                    parsed.highlights ?? []
                );
            } catch {
                // Fallback: resposta já é texto puro
                this.statsService.setGameSummary(response.answer, []);
            }
        } catch (error) {
            console.error('Erro ao carregar resumo IA:', error);
            this.statsService.setGameSummary('Não foi possível gerar o resumo automático para este jogo no momento.');
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
            let valA = a[field];
            let valB = b[field];

            // Tratamento especial para strings ou nulos
            if (typeof valA === 'string' && !isNaN(Number(valA))) valA = Number(valA);
            if (typeof valB === 'string' && !isNaN(Number(valB))) valB = Number(valB);
            
            valA = valA ?? 0;
            valB = valB ?? 0;

            if (typeof valA === 'string' && typeof valB === 'string') {
                return direction === 'asc' 
                    ? valA.localeCompare(valB) 
                    : valB.localeCompare(valA);
            }

            return direction === 'asc' ? valA - valB : valB - valA;
        });
    }

    getPlayerAvatar(espnId: string | number | undefined | null): string {
        if (!espnId || espnId === '0' || espnId === 0) {
            return 'assets/player-placeholder.png';
        }
        // Nota: O espnId deve ser usado para fotos reais da ESPN
        return `https://a.espncdn.com/i/headshots/nba/players/full/${espnId}.png`;
    }

    onImageError(event: Event): void {
        const img = event.target as HTMLImageElement;
        if (!img.src.includes('player-placeholder.png')) {
            console.log('Erro ao carregar avatar, usando placeholder');
            img.src = 'assets/player-placeholder.png';
        }
    }

    getHighlightIcon(type: string): string {
        switch (type?.toLowerCase()) {
            case 'info': return 'info';
            case 'warning': return 'warning';
            case 'success': return 'check_circle';
            case 'star': return 'auto_awesome';
            case 'trending_up': return 'trending_up';
            case 'trending_down': return 'trending_down';
            default: return 'bolt';
        }
    }

    getTeamLogo(abbr: string): string {
        if (!abbr) return '';
        return `https://a.espncdn.com/combiner/i?img=/i/teamlogos/nba/500/${abbr.toUpperCase()}.png&w=120&h=120&transparent=true`;
    }

    setBoxscoreTeam(team: 'visitor' | 'home'): void {
        this.boxscoreTeamView.set(team);
    }

    getFgPct(totals: { fieldGoalsMade?: number; fieldGoalsAttempted?: number } | undefined | null): string {
        if (!totals || !totals.fieldGoalsAttempted) return '—';
        return ((totals.fieldGoalsMade! / totals.fieldGoalsAttempted) * 100).toFixed(1);
    }
}
