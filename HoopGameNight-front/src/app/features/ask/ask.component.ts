import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { AssistantComponent } from '../../shared/components/assistant/assistant.component';
import { SEOService } from '../../core/services/seo.service';

@Component({
  selector: 'app-ask',
  standalone: true,
  imports: [CommonModule, AssistantComponent],
  templateUrl: './ask.component.html',
  styleUrls: ['./ask.component.scss']
})
export class AskComponent implements OnInit {
  private readonly seoService = inject(SEOService);

  ngOnInit(): void {
    this.seoService.updateTitle('IA Coach Assistant | HoopGameNight');
    this.seoService.updateMeta('Perunte ao assistente inteligente sobre jogos, atletas e estatísticas da NBA.');
  }
}
