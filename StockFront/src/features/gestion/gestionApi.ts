import { api } from '../../lib/api';
import type { Rol, UsuarioInfo } from '../auth/types';

// --- Comensales y presupuestos ---
export interface Comensal {
  hotelId: number;
  hotel: string;
  anio: number;
  mes: number;
  numeroComensales: number;
}

export interface Presupuesto {
  hotelId: number;
  hotel: string;
  categoria: string;
  anio: number;
  mes: number;
  monto: number;
}

export const listarComensales = (anio: number, mes: number) =>
  api.get<Comensal[]>('/api/gestion/comensales', { params: { anio, mes } }).then((r) => r.data);

export const guardarComensal = (data: { hotelId: number; anio: number; mes: number; numeroComensales: number }) =>
  api.put('/api/gestion/comensales', data);

export const listarPresupuestos = (anio: number, mes: number) =>
  api.get<Presupuesto[]>('/api/gestion/presupuestos', { params: { anio, mes } }).then((r) => r.data);

export const guardarPresupuesto = (data: { hotelId: number; categoria: string; anio: number; mes: number; monto: number }) =>
  api.put('/api/gestion/presupuestos', data);

// --- Usuarios (Admin) ---
export interface UsuarioLista {
  id: string;
  nombre: string;
  email: string;
  roles: Rol[];
  hoteles: number[];
  activo: boolean;
}

export const listarUsuarios = () =>
  api.get<UsuarioLista[]>('/api/auth/usuarios').then((r) => r.data);

export const crearUsuario = (data: {
  nombre: string;
  email: string;
  password: string;
  rol: Rol;
  hoteles?: number[];
}) => api.post<UsuarioInfo>('/api/auth/usuarios', data).then((r) => r.data);

export const cambiarActivoUsuario = (id: string, activo: boolean) =>
  api.put(`/api/auth/usuarios/${id}/activo`, { activo });
