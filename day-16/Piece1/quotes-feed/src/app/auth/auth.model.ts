export interface LoginRequest {
  email: string;
  password: string;
}

export interface LoginResponse {
  access_token: string;
  // The refresh token is never sent in the body -- the backend sets it as an
  // HttpOnly cookie instead, so JavaScript can never read it.
  expires_in: number;
}