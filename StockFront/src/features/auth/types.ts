export type Rol = 'Admin' | 'Gerencia' | 'Digitador' | 'SoloLectura';

export interface UsuarioInfo {
  id: string;
  nombre: string;
  email: string;
  roles: Rol[];
  hoteles: number[];
}

export interface AuthResponse {
  accessToken: string;
  expiraEn: string;
  refreshToken: string;
  usuario: UsuarioInfo;
}
