import { Component, OnInit, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { PlayersService } from '../../../core/services/players.service';
import { StatLeader } from '../../../core/interfaces/api.interface';
import { RouterModule } from '@angular/router';

@Component({
    selector: 'app-league-leaders',
    standalone: true,
    imports: [CommonModule, RouterModule],
    templateUrl: './league-leaders.component.html',
    styleUrls: ['./league-leaders.component.scss']
})
export class LeagueLeadersComponent implements OnInit {
    private readonly playersService = inject(PlayersService);

    readonly leaders = this.playersService.leagueLeaders;
    readonly isLoading = this.playersService.isLoading;
    readonly activeTab = signal<'pts' | 'reb' | 'ast' | 'stl' | 'blk' | '3p'>('pts');

    ngOnInit(): void {
        if (!this.leaders()) {
            this.playersService.loadLeagueLeaders();
        }
    }

    setTab(tab: 'pts' | 'reb' | 'ast' | 'stl' | 'blk' | '3p'): void {
        this.activeTab.set(tab);
    }

    getLeadersByTab(): StatLeader[] {
        const data = this.leaders();
        if (!data) return [];

        switch (this.activeTab()) {
            case 'pts': return data.scoringLeaders;
            case 'reb': return data.reboundLeaders;
            case 'ast': return data.assistLeaders;
            case 'stl': return data.stealsLeaders;
            case 'blk': return data.blocksLeaders;
            case '3p': return data.threePointLeaders;
            default: return [];
        }
    }

    getTabLabel(): string {
        switch (this.activeTab()) {
            case 'pts': return 'Pontos';
            case 'reb': return 'Rebotes';
            case 'ast': return 'Assistências';
            case 'stl': return 'Roubos';
            case 'blk': return 'Tocos';
            case '3p': return '3 Pontos';
            default: return '';
        }
    }

    getPlayerPhoto(leader: StatLeader): string {
        return this.playersService.getPlayerPhotoUrl(leader.externalId || leader.playerId);
    }
}
