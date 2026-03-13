import { Component, inject, OnInit } from '@angular/core';
import { SwUpdate } from '@angular/service-worker';
import { MainLayout } from './layout/main-layout/main-layout';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [MainLayout],
  template: `<app-main-layout></app-main-layout>`
})
export class App implements OnInit {
  private swUpdate = inject(SwUpdate);

  ngOnInit() {
    if (this.swUpdate.isEnabled) {
      this.swUpdate.versionUpdates.subscribe(event => {
        if (event.type === 'VERSION_READY') {
          if (confirm('Nova versão do HoopGameNight disponível. Deseja atualizar agora para obter as últimas novidades e correções?')) {
            window.location.reload();
          }
        }
      });
    }
  }
}