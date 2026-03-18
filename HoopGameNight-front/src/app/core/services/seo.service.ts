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

    updateMeta(description: string = this.defaultDesc, keywords: string = 'NBA, basquete, estatísticas, jogos ao vivo, análise de desempenho', image: string = 'assets/icons/icon-512x512.png'): void {
        this.metaService.updateTag({ name: 'description', content: description });
        this.metaService.updateTag({ name: 'keywords', content: keywords });

        // Open Graph
        const currentTitle = this.titleService.getTitle();
        this.metaService.updateTag({ property: 'og:title', content: currentTitle });
        this.metaService.updateTag({ property: 'og:description', content: description });
        this.metaService.updateTag({ property: 'og:type', content: 'website' });
        this.metaService.updateTag({ property: 'og:image', content: image });
        this.metaService.updateTag({ property: 'og:url', content: window.location.href });

        // Twitter Cards
        this.metaService.updateTag({ name: 'twitter:card', content: 'summary_large_image' });
        this.metaService.updateTag({ name: 'twitter:title', content: currentTitle });
        this.metaService.updateTag({ name: 'twitter:description', content: description });
        this.metaService.updateTag({ name: 'twitter:image', content: image });

        this.updateCanonicalLink();
    }

    private updateCanonicalLink(): void {
        let link: HTMLLinkElement | null = document.querySelector('link[rel="canonical"]');
        if (!link) {
            link = document.createElement('link');
            link.setAttribute('rel', 'canonical');
            document.head.appendChild(link);
        }
        link.setAttribute('href', window.location.href);
    }

    reset(): void {
        this.updateTitle();
        this.updateMeta();
    }
}
