import { api } from '../../lib/api';
import type { Conversion, Hotel, Producto, Proveedor, Unidad } from './types';

// --- Productos ---
export const listarProductos = (soloActivos = true) =>
  api.get<Producto[]>('/api/productos', { params: { soloActivos } }).then((r) => r.data);

export const crearProducto = (data: { nombre: string; categoria: string; unidadBaseId: number }) =>
  api.post<Producto>('/api/productos', data).then((r) => r.data);

export const actualizarProducto = (
  id: number,
  data: { nombre: string; categoria: string; unidadBaseId: number; activo: boolean },
) => api.put<Producto>(`/api/productos/${id}`, data).then((r) => r.data);

export const listarConversiones = (productoId: number) =>
  api.get<Conversion[]>(`/api/productos/${productoId}/conversiones`).then((r) => r.data);

export const agregarConversion = (productoId: number, data: { unidadId: number; factorABase: number }) =>
  api.post<Conversion>(`/api/productos/${productoId}/conversiones`, data).then((r) => r.data);

// --- Proveedores ---
export const listarProveedores = (soloActivos = true) =>
  api.get<Proveedor[]>('/api/proveedores', { params: { soloActivos } }).then((r) => r.data);

export const crearProveedor = (data: { nombre: string; nit?: string; telefono?: string; diasCredito?: number }) =>
  api.post<Proveedor>('/api/proveedores', data).then((r) => r.data);

export const actualizarProveedor = (
  id: number,
  data: { nombre: string; nit?: string; telefono?: string; diasCredito: number; activo: boolean },
) => api.put<Proveedor>(`/api/proveedores/${id}`, data).then((r) => r.data);

// --- Auxiliares ---
export const listarUnidades = () => api.get<Unidad[]>('/api/catalogos/unidades').then((r) => r.data);

export const crearUnidad = (data: { nombre: string; abreviatura: string }) =>
  api.post<Unidad>('/api/catalogos/unidades', data).then((r) => r.data);

export const listarHoteles = (soloActivos = true) =>
  api.get<Hotel[]>('/api/catalogos/hoteles', { params: { soloActivos } }).then((r) => r.data);
