import { Component, Input, OnInit, OnChanges, SimpleChanges, ViewChild, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ChartConfiguration, ChartData, ChartType, Chart } from 'chart.js';
import { BaseChartDirective } from 'ng2-charts';

@Component({
    selector: 'app-player-performance-chart',
    standalone: true,
    imports: [CommonModule, BaseChartDirective],
    templateUrl: './player-performance-chart.html',
    styleUrl: './player-performance-chart.scss'
})
export class PlayerPerformanceChartComponent implements OnInit, OnChanges {
    @Input() games: any[] = [];
    @Input() teamColor: string = '#3b82f6'; // Default blue

    @ViewChild(BaseChartDirective) chart?: BaseChartDirective;

    public activeMetric = signal<'points' | 'rebounds' | 'assists'>('points');

    public lineChartData: ChartData<'line'> = {
        labels: [],
        datasets: [
            {
                data: [],
                label: 'Pontos',
                backgroundColor: 'rgba(59, 130, 246, 0.2)',
                borderColor: '#3b82f6',
                pointBackgroundColor: '#3b82f6',
                pointBorderColor: '#fff',
                pointHoverBackgroundColor: '#fff',
                pointHoverBorderColor: '#3b82f6',
                fill: 'origin',
                tension: 0.4,
            }
        ]
    };

    public lineChartOptions: ChartConfiguration['options'] = {
        responsive: true,
        maintainAspectRatio: false,
        elements: {
            line: {
                tension: 0.4
            }
        },
        scales: {
            y: {
                beginAtZero: true,
                grid: {
                    color: 'rgba(255, 255, 255, 0.05)',
                },
                ticks: {
                    color: 'rgba(255, 255, 255, 0.6)',
                    font: {
                        size: 10
                    }
                }
            },
            x: {
                grid: {
                    display: false
                },
                ticks: {
                    color: 'rgba(255, 255, 255, 0.6)',
                    font: {
                        size: 10
                    },
                    maxRotation: 45,
                    minRotation: 45
                }
            }
        },
        plugins: {
            legend: { display: false },
            tooltip: {
                backgroundColor: 'rgba(0, 0, 0, 0.8)',
                titleColor: '#fff',
                bodyColor: '#fff',
                borderColor: 'rgba(255, 255, 255, 0.1)',
                borderWidth: 1,
                padding: 10,
                displayColors: false,
                callbacks: {
                    label: (context) => {
                        return `${context.dataset.label}: ${context.parsed.y}`;
                    }
                }
            }
        }
    };

    public lineChartType: ChartType = 'line';

    ngOnInit() {
        this.updateChartData();
    }

    ngOnChanges(changes: SimpleChanges) {
        if (changes['games'] || changes['teamColor']) {
            this.updateChartData();
        }
    }

    setMetric(metric: 'points' | 'rebounds' | 'assists') {
        this.activeMetric.set(metric);
        this.updateChartData();
    }

    private updateChartData() {
        if (!this.games || this.games.length === 0) {
            this.lineChartData.labels = [];
            this.lineChartData.datasets[0].data = [];
            if (this.chart) this.chart.update();
            return;
        }

        // Filter only games with valid dates for the chart
        const gamesWithDates = this.games.filter(g => {
            const d = g.gameDate || g.date || g.dateTime;
            if (!d) return false;
            const dateObj = new Date(d);
            return !isNaN(dateObj.getTime()) && dateObj.getFullYear() >= 1990;
        });

        if (gamesWithDates.length === 0) {
            this.lineChartData.labels = [];
            this.lineChartData.datasets[0].data = [];
            if (this.chart) this.chart.update();
            return;
        }

        // We show all games provided to see the full "evolution"
        // Valid games for sorting purposes (but we keep all)
        const sortedGames = [...gamesWithDates].sort((a, b) => {
            const dA = a.gameDate || a.date || a.dateTime;
            const dB = b.gameDate || b.date || b.dateTime;
            const timeA = dA ? new Date(dA).getTime() : 0;
            const timeB = dB ? new Date(dB).getTime() : 0;
            return timeA - timeB;
        });

        // Show all games provided for a full season view
        const displayGames = sortedGames;

        const labels = displayGames.map(g => {
            const d = g.gameDate || g.date || g.dateTime;
            if (!d) return '??/??';

            const dateObj = new Date(d);
            // Year 0001 or 1970 usually means missing data
            if (isNaN(dateObj.getTime()) || dateObj.getFullYear() < 1990) {
                return '??/??';
            }

            // Manual parsing for ISO stability (YYYY-MM-DD or DD-MM-YYYY)
            if (typeof d === 'string' && d.includes('-')) {
                const datePart = d.split('T')[0];
                const parts = datePart.split('-');
                if (parts.length === 3) {
                    let day = '01', month = '01';
                    if (parts[0].length === 4) { // YYYY-MM-DD
                        day = parts[2]; month = parts[1];
                    } else { // assume DD-MM-YYYY
                        day = parts[0]; month = parts[1];
                    }
                    return `${day.toString().padStart(2, '0')}/${month.toString().padStart(2, '0')}`;
                }
            }

            const day = dateObj.getDate().toString().padStart(2, '0');
            const month = (dateObj.getMonth() + 1).toString().padStart(2, '0');
            return `${day}/${month}`;
        });

        let chartData: number[] = [];
        let chartLabel = '';

        switch (this.activeMetric()) {
            case 'points':
                chartData = displayGames.map(g => Number(g.points || 0));
                chartLabel = 'Pontos';
                break;
            case 'rebounds':
                chartData = displayGames.map(g => Number(g.rebounds || g.totalRebounds || 0));
                chartLabel = 'Rebotes';
                break;
            case 'assists':
                chartData = displayGames.map(g => Number(g.assists || 0));
                chartLabel = 'Assistências';
                break;
        }

        // Update properties instead of replacing object (better for local Chart.js state)
        this.lineChartData.labels = labels;
        this.lineChartData.datasets[0].data = chartData;
        this.lineChartData.datasets[0].label = chartLabel;
        this.lineChartData.datasets[0].backgroundColor = this.getGradientFill();
        this.lineChartData.datasets[0].borderColor = this.teamColor;
        this.lineChartData.datasets[0].pointBackgroundColor = this.teamColor;
        this.lineChartData.datasets[0].pointHoverBorderColor = this.teamColor;

        if (this.chart) {
            this.chart.update();
        }
    }

    private getGradientFill() {
        // This is tricky with Chart.js outside of a canvas context, 
        // but we can return a function or a string color for now.
        // In BaseChartDirective, it better supports color strings.
        // We'll use a semi-transparent version of the team color.
        return `${this.teamColor}33`; // 33 is ~20% opacity in hex
    }
}
