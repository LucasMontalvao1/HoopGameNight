import { Injectable, inject } from '@angular/core';
import { Title, Meta } from '@angular/platform-browser';

@Injectable({
    providedIn: 'root'
})
export class SEOService {
    private readonly titleService = inject(Title);
    private readonly metaService = inject(Meta);

    private readonly defaultTitle = 'HoopGameNight - NBA Insights & Stats';
    private readonly defaultDesc = 'Acompanhe jogos da NBA em tempo real, estatísticas de jogadores e insights inteligentes com análises de desempenho.';

    updateTitle(newTitle?: string): void {
        const fullTitle = newTitle ? `${newTitle} | HoopGameNight` : this.defaultTitle;
        this.titleService.setTitle(fullTitle);
    }

    updateMeta(description: string = this.defaultDesc, keywords: string = 'NBA, basquete, estatísticas, jogos ao vivo, análise de desempenho'): void {
        this.metaService.updateTag({ name: 'description', content: description });
        this.metaService.updateTag({ name: 'keywords', content: keywords });

        // Open Graph
        this.metaService.updateTag({ property: 'og:title', content: this.titleService.getTitle() });
        this.metaService.updateTag({ property: 'og:description', content: description });
        this.metaService.updateTag({ property: 'og:type', content: 'website' });
    }

    reset(): void {
        this.updateTitle();
        this.updateMeta();
    }
}
