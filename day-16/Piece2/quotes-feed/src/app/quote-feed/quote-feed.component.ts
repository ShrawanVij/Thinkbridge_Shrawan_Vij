import { Component, inject } from '@angular/core';
import { DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { QuoteFeedStore } from './quote-feed.store';

@Component({
  selector: 'app-quote-feed',
  imports: [DatePipe, RouterLink],
  templateUrl: './quote-feed.component.html',
  styleUrl: './quote-feed.component.css',
})
export class QuoteFeedComponent {
  private readonly store = inject(QuoteFeedStore);

  readonly quotes = this.store.quotes;
  readonly searchTerm = this.store.searchTerm;
  readonly loading = this.store.loading;
  readonly error = this.store.error;
  readonly pageSize = this.store.pageSize;
  readonly sortOrder = this.store.sortOrder;
  readonly filteredQuotes = this.store.filteredQuotes;
  readonly availableAuthors = this.store.availableAuthors;

  onSearchChange(value: string): void {
    this.store.setSearchTerm(value);
  }

  selectAuthor(author: string): void {
    this.store.selectAuthor(author);
  }

  onPageSizeChange(value: string): void {
    this.store.setPageSize(value);
  }

  onSortOrderChange(value: string): void {
    this.store.setSortOrder(value);
  }

  onDelete(id: number): void {
    if (!confirm('Delete this quote?')) {
      return;
    }
    this.store.deleteQuote(id);
  }
}