export interface QuoteFeedItem {
  id: number;
  author: string;
  text: string;
  createdAt: string;
  tags: string;
}

export interface Tag {
  id: number;
  name: string;
}

export interface QuoteDetail {
  id: number;
  author: string;
  text: string;
  userId: number;
  createdAt: string;
  tags: Tag[];
}
