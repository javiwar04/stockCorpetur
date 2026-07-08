import axios, {
  AxiosError,
  type AxiosRequestConfig,
  type InternalAxiosRequestConfig,
} from 'axios';
import { useAuth } from '../features/auth/authStore';
import type { AuthResponse } from '../features/auth/types';

const apiBaseUrl = import.meta.env.VITE_API_URL?.trim().replace(/\/+$/, '') || '/';
const apiUrl = (path: string) => (apiBaseUrl === '/' ? path : `${apiBaseUrl}${path}`);

// En dev, Vite proxea /api al backend. En produccion, VITE_API_URL apunta al VPS.
export const api = axios.create({ baseURL: apiBaseUrl });

// Adjunta el access token a cada petición.
api.interceptors.request.use((config: InternalAxiosRequestConfig) => {
  const token = useAuth.getState().accessToken;
  if (token) config.headers.Authorization = `Bearer ${token}`;
  return config;
});

// Maneja 401 renovando con el refresh token una sola vez.
let refreshing: Promise<string | null> | null = null;

async function renovarToken(): Promise<string | null> {
  const { refreshToken, usuario, setSesion, logout } = useAuth.getState();
  if (!refreshToken) return null;
  try {
    const { data } = await axios.post<AuthResponse>(apiUrl('/api/auth/refresh'), { refreshToken });
    setSesion(data.accessToken, data.refreshToken, data.usuario ?? usuario!);
    return data.accessToken;
  } catch {
    logout();
    return null;
  }
}

api.interceptors.response.use(
  (r) => r,
  async (error: AxiosError) => {
    const original = error.config as (AxiosRequestConfig & { _retry?: boolean }) | undefined;
    const esAuth = original?.url?.includes('/api/auth/');

    if (error.response?.status === 401 && original && !original._retry && !esAuth) {
      original._retry = true;
      refreshing ??= renovarToken();
      const nuevo = await refreshing;
      refreshing = null;
      if (nuevo) {
        original.headers = { ...original.headers, Authorization: `Bearer ${nuevo}` };
        return api(original);
      }
    }
    return Promise.reject(error);
  },
);
