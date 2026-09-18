export interface CollectionItem {
  quoteId: number;
  addedAt: string;
}

export interface Collection {
  id: number;
  name: string;
  ownerId: number;
  items: CollectionItem[];
}
