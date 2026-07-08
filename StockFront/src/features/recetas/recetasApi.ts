import { api } from '../../lib/api';

export interface Ingrediente {
  id: number;
  productoId: number;
  producto: string;
  unidadBase: string;
  cantidadPorPorcion: number;
  precioUnitario: number;
  costoLinea: number;
  tienePrecio: boolean;
}

export interface Plato {
  id: number;
  nombre: string;
  precioVenta: number | null;
  activo: boolean;
  costo: number;
  costoCompleto: boolean;
  margen: number | null;
  foodCostPorcentaje: number | null;
  ingredientes: Ingrediente[];
}

export interface ImpactoPlato {
  platoId: number;
  plato: string;
  cantidadPorPorcion: number;
  costoLinea: number;
  costoPlato: number;
  porcentajeDelCosto: number;
}

export const listarPlatos = (soloActivos = true) =>
  api.get<Plato[]>('/api/platos', { params: { soloActivos } }).then((r) => r.data);

export const crearPlato = (data: { nombre: string; precioVenta?: number }) =>
  api.post<Plato>('/api/platos', data).then((r) => r.data);

export const actualizarPlato = (id: number, data: { nombre: string; precioVenta?: number; activo: boolean }) =>
  api.put<Plato>(`/api/platos/${id}`, data).then((r) => r.data);

export const upsertIngrediente = (platoId: number, data: { productoId: number; cantidadPorPorcion: number }) =>
  api.post<Plato>(`/api/platos/${platoId}/ingredientes`, data).then((r) => r.data);

export const eliminarIngrediente = (platoId: number, ingredienteId: number) =>
  api.delete<Plato>(`/api/platos/${platoId}/ingredientes/${ingredienteId}`).then((r) => r.data);

export const impactoProducto = (productoId: number) =>
  api.get<ImpactoPlato[]>(`/api/platos/impacto/${productoId}`).then((r) => r.data);
