import { Injectable, signal } from '@angular/core';

export interface Toast {
  id: number;
  kind: 'error' | 'info';
  title: string;
  detail?: string;
}

export const TOAST_DURATION_MS = 5000;

@Injectable({ providedIn: 'root' })
export class ToastStore {
  private readonly state = signal<Toast[]>([]);
  private nextId = 0;

  readonly toasts = this.state.asReadonly();

  show(toast: Omit<Toast, 'id'>): void {
    const id = ++this.nextId;
    this.state.update((list) => [...list, { ...toast, id }]);
    setTimeout(() => this.dismiss(id), TOAST_DURATION_MS);
  }

  dismiss(id: number): void {
    this.state.update((list) => list.filter((t) => t.id !== id));
  }
}
