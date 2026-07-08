import { create } from 'zustand';
import { persist } from 'zustand/middleware';
import type { Rol, UsuarioInfo } from './types';

interface AuthState {
  accessToken: string | null;
  refreshToken: string | null;
  usuario: UsuarioInfo | null;
  setSesion: (accessToken: string, refreshToken: string, usuario: UsuarioInfo) => void;
  setTokens: (accessToken: string, refreshToken: string) => void;
  logout: () => void;
  tieneRol: (...roles: Rol[]) => boolean;
}

export const useAuth = create<AuthState>()(
  persist(
    (set, get) => ({
      accessToken: null,
      refreshToken: null,
      usuario: null,
      setSesion: (accessToken, refreshToken, usuario) =>
        set({ accessToken, refreshToken, usuario }),
      setTokens: (accessToken, refreshToken) => set({ accessToken, refreshToken }),
      logout: () => set({ accessToken: null, refreshToken: null, usuario: null }),
      tieneRol: (...roles) => {
        const u = get().usuario;
        return !!u && roles.some((r) => u.roles.includes(r));
      },
    }),
    { name: 'stockcontrol-auth' },
  ),
);
