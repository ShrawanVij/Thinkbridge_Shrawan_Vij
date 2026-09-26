import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { CollectionService } from './collection.service';
import { Collection } from './collection.model';

@Component({
  selector: 'app-collections-page',
  standalone: true,
  imports: [FormsModule, RouterLink],
  templateUrl: './collections-page.component.html',
})
export class CollectionsPageComponent {
  private readonly collectionService = inject(CollectionService);

  readonly collections = signal<Collection[]>([]);
  readonly loading = signal(true);
  readonly newName = signal('');

  constructor() {
    this.load();
  }

  private load(): void {
    this.loading.set(true);
    this.collectionService.getMine().subscribe({
      next: (collections) => {
        this.collections.set(collections);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  create(): void {
    const name = this.newName().trim();
    if (!name) return;

    this.collectionService.create(name).subscribe(() => {
      this.newName.set('');
      this.load();
    });
  }

  delete(id: number): void {
    if (!confirm('Delete this collection? This cannot be undone.')) return;

    this.collectionService.delete(id).subscribe(() => this.load());
  }
}
