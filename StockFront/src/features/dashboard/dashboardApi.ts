import { api } from '../../lib/api';
import type {
  AlertaPrecio,
  ConsumoHotelSerie,
  DashboardGerencial,
  ResumenMensual,
  TendenciaPrecio,
  TopProducto,
} from './types';

export const obtenerResumen = (anio?: number, mes?: number) =>
  api.get<ResumenMensual>('/api/dashboard/resumen', { params: { anio, mes } }).then((r) => r.data);

export const topComprados = (meses = 6, top = 10) =>
  api.get<TopProducto[]>('/api/dashboard/top-comprados', { params: { meses, top } }).then((r) => r.data);

export const topCaros = (meses = 6, top = 10) =>
  api.get<TopProducto[]>('/api/dashboard/top-caros', { params: { meses, top } }).then((r) => r.data);

export const tendenciaPrecio = (productoId: number, meses = 12) =>
  api.get<TendenciaPrecio>(`/api/dashboard/tendencia-precio/${productoId}`, { params: { meses } }).then((r) => r.data);

export const consumoHoteles = (meses = 6) =>
  api.get<ConsumoHotelSerie[]>('/api/dashboard/consumo-hoteles', { params: { meses } }).then((r) => r.data);

export const alertasPrecio = (umbral = 15) =>
  api.get<AlertaPrecio[]>('/api/dashboard/alertas', { params: { umbral } }).then((r) => r.data);

export const obtenerGerencial = (anio?: number, mes?: number) =>
  api.get<DashboardGerencial>('/api/dashboard/gerencial', { params: { anio, mes } }).then((r) => r.data);
