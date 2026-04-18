import { Component, inject, ViewChild, ElementRef, AfterViewChecked } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { AssistantService } from '../../../core/services/assistant.service';

@Component({
  selector: 'app-assistant',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './assistant.component.html',
  styleUrls: ['./assistant.component.scss']
})
export class AssistantComponent implements AfterViewChecked {
  protected readonly assistantService = inject(AssistantService);
  
  @ViewChild('chatContainer') private chatContainer!: ElementRef;
  @ViewChild('questionInput') private questionInput!: ElementRef;

  questionText = '';

  async askAssistant() {
    const q = this.questionText;
    if (!q || !q.trim() || this.assistantService.isTyping()) return;
    
    this.questionText = '';
    await this.assistantService.sendMessage(q);
  }

  ngAfterViewChecked() {
    this.scrollToBottom();
  }

  private scrollToBottom(): void {
    try {
      this.chatContainer.nativeElement.scrollTop = this.chatContainer.nativeElement.scrollHeight;
    } catch (err) {}
  }
}
