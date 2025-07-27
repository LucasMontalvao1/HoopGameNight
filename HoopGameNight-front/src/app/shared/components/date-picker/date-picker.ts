import { Component, EventEmitter, Input, Output, signal } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-date-picker',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './date-picker.html',
  styleUrls: ['./date-picker.scss']
})
export class DatePicker {
  @Input() selectedDate: Date = new Date();
  @Input() minDate?: Date;
  @Input() maxDate?: Date;
  @Output() dateChange = new EventEmitter<Date>();

  private readonly _isOpen = signal<boolean>(false);
  private readonly _currentMonth = signal<Date>(new Date());

  readonly isOpen = this._isOpen.asReadonly();
  readonly currentMonth = this._currentMonth.asReadonly();

  toggleCalendar(): void {
    this._isOpen.update(current => !current);
  }

  closeCalendar(): void {
    this._isOpen.set(false);
  }

  selectDate(date: Date): void {
    this.selectedDate = date;
    this.dateChange.emit(date);
    this.closeCalendar();
  }

  navigateMonth(direction: 'prev' | 'next'): void {
    const current = this._currentMonth();
    const newMonth = new Date(current);
    
    if (direction === 'prev') {
      newMonth.setMonth(current.getMonth() - 1);
    } else {
      newMonth.setMonth(current.getMonth() + 1);
    }
    
    this._currentMonth.set(newMonth);
  }

  goToToday(): void {
    const today = new Date();
    this._currentMonth.set(today);
    this.selectDate(today);
  }

  getMonthDays(): Date[] {
    const current = this._currentMonth();
    const year = current.getFullYear();
    const month = current.getMonth();
    
    const firstDay = new Date(year, month, 1);
    const lastDay = new Date(year, month + 1, 0);
    const startDate = new Date(firstDay);
    
    startDate.setDate(startDate.getDate() - (startDate.getDay() || 7) + 1);
    
    const days: Date[] = [];
    const current_date = new Date(startDate);
    
    for (let i = 0; i < 42; i++) { // 6 semanas × 7 dias
      days.push(new Date(current_date));
      current_date.setDate(current_date.getDate() + 1);
    }
    
    return days;
  }

  isToday(date: Date): boolean {
    const today = new Date();
    return date.toDateString() === today.toDateString();
  }

  isSelected(date: Date): boolean {
    return date.toDateString() === this.selectedDate.toDateString();
  }

  isCurrentMonth(date: Date): boolean {
    const current = this._currentMonth();
    return date.getMonth() === current.getMonth() && 
           date.getFullYear() === current.getFullYear();
  }

  isDisabled(date: Date): boolean {
    if (this.minDate && date < this.minDate) return true;
    if (this.maxDate && date > this.maxDate) return true;
    return false;
  }

  getFormattedDate(): string {
    return this.selectedDate.toLocaleDateString('pt-BR', {
      weekday: 'short',
      day: '2-digit',
      month: 'short'
    });
  }

  getMonthYear(): string {
    return this._currentMonth().toLocaleDateString('pt-BR', {
      month: 'long',
      year: 'numeric'
    });
  }
}