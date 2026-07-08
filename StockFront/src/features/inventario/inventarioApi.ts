import { api } from '../../lib/api';

export interface Existencia {
  productoId: number;
  producto: string;
  categoria: string;
  unidadBase: string;
  comprado: number;
  salidas: number;
  mermas: number;
  ajustes: number;
  existencia: number;
  stockMinimo: number;
  faltante: number;
  estadoStock: 'Ok' | 'BajoMinimo' | 'SinStock' | 'Negativo' | 'SinConfigurar';
}

export interface Movimiento {
  id: number;
  tipo: 'Entrada' | 'Salida' | 'Merma' | 'Ajuste';
  fecha: string;
  hotelId: number;
  hotel: string;
  productoId: number;
  producto: string;
  unidadBase: string;
  cantidadBase: number;
  referencia: string | null;
  creadoPor: string | null;
}

export interface CrearMovimientoRequest {
  tipo: string;
  fecha: string;
  hotelId: number;
  productoId: number;
  unidadId: number;
  cantidad: number;
  referencia?: string;
}

export interface StockMinimo {
  hotelId: number;
  productoId: number;
  producto: string;
  unidadBase: string;
  cantidadMinimaBase: number;
}

export interface AlertaStock {
  hotelId: number;
  hotel: string;
  productoId: number;
  producto: string;
  categoria: string;
  unidadBase: string;
  existencia: number;
  stockMinimo: number;
  faltante: number;
  estadoStock: Existencia['estadoStock'];
}

export interface SugerenciaCompra {
  hotelId: number;
  hotel: string;
  productoId: number;
  producto: string;
  categoria: string;
  unidadBase: string;
  cantidadSugeridaBase: number;
  existencia: number;
  stockMinimo: number;
  ultimoPrecioBase: number | null;
  proveedorId: number | null;
  proveedorNombre: string | null;
  ultimaCompra: string | null;
  costoEstimado: number | null;
}

export interface KardexMovimiento {
  id: string;
  fecha: string;
  tipo: 'Compra' | 'Entrada' | 'Salida' | 'Merma' | 'Ajuste';
  referencia: string;
  entrada: number;
  salida: number;
  ajuste: number;
  saldo: number;
  costoUnitario: number | null;
  costoTotal: number | null;
  documento: string | null;
  proveedor: string | null;
  creadoPor: string | null;
}

export interface Kardex {
  hotelId: number;
  hotel: string;
  productoId: number;
  producto: string;
  unidadBase: string;
  desde: string | null;
  hasta: string | null;
  saldoInicial: number;
  totalEntradas: number;
  totalSalidas: number;
  totalAjustes: number;
  saldoFinal: number;
  movimientos: KardexMovimiento[];
}

export const obtenerExistencias = (hotelId: number) =>
  api.get<Existencia[]>(`/api/inventario/existencias/${hotelId}`).then((r) => r.data);

export const listarAlertasStock = () =>
  api.get<AlertaStock[]>('/api/inventario/alertas-stock').then((r) => r.data);

export const sugerenciasCompra = (hotelId: number) =>
  api.get<SugerenciaCompra[]>(`/api/inventario/sugerencias-compra/${hotelId}`).then((r) => r.data);

export const obtenerKardex = (params: { hotelId: number; productoId: number; desde?: string; hasta?: string }) =>
  api.get<Kardex>('/api/inventario/kardex', { params }).then((r) => r.data);

export async function descargarKardexExcel(params: { hotelId: number; productoId: number; desde?: string; hasta?: string }) {
  const { data } = await api.get<Blob>('/api/reportes/kardex.xlsx', { params, responseType: 'blob' });
  const enlace = document.createElement('a');
  enlace.href = URL.createObjectURL(data);
  enlace.download = `kardex-${new Date().toISOString().slice(0, 10)}.xlsx`;
  enlace.click();
  URL.revokeObjectURL(enlace.href);
}

export const listarStockMinimo = (hotelId: number) =>
  api.get<StockMinimo[]>(`/api/inventario/stock-minimo/${hotelId}`).then((r) => r.data);

export const guardarStockMinimo = (data: { hotelId: number; productoId: number; cantidadMinimaBase: number }) =>
  api.put<StockMinimo>('/api/inventario/stock-minimo', data).then((r) => r.data);

export const eliminarStockMinimo = (hotelId: number, productoId: number) =>
  api.delete(`/api/inventario/stock-minimo/${hotelId}/${productoId}`);

export const listarMovimientos = (params?: { hotelId?: number; productoId?: number }) =>
  api.get<Movimiento[]>('/api/inventario/movimientos', { params }).then((r) => r.data);

export const registrarMovimiento = (data: CrearMovimientoRequest) =>
  api.post<Movimiento>('/api/inventario/movimientos', data).then((r) => r.data);

export const eliminarMovimiento = (id: number) => api.delete(`/api/inventario/movimientos/${id}`);
