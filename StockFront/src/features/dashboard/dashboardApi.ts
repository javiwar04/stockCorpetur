import { api } from '../../lib/api';
import type {
  AlertaPrecio,
  ConsumoHotelSerie,
  DashboardGerencial,
  ResumenMensual,
  TendenciaPrecio,
  TopProducto,
} from './types';

export const obtenerResumen = (anio?: number, mes?: number, hotelId?: number) =>
  api.get<ResumenMensual>('/api/dashboard/resumen', { params: { anio, mes, hotelId } }).then((r) => r.data);

export const topComprados = (meses = 6, top = 10, hotelId?: number) =>
  api.get<TopProducto[]>('/api/dashboard/top-comprados', { params: { meses, top, hotelId } }).then((r) => r.data);

export const topCaros = (meses = 6, top = 10, hotelId?: number) =>
  api.get<TopProducto[]>('/api/dashboard/top-caros', { params: { meses, top, hotelId } }).then((r) => r.data);

export const tendenciaPrecio = (productoId: number, meses = 12, hotelId?: number) =>
  api.get<TendenciaPrecio>(`/api/dashboard/tendencia-precio/${productoId}`, { params: { meses, hotelId } }).then((r) => r.data);

export const consumoHoteles = (meses = 6, hotelId?: number) =>
  api.get<ConsumoHotelSerie[]>('/api/dashboard/consumo-hoteles', { params: { meses, hotelId } }).then((r) => r.data);

export const alertasPrecio = (umbral = 15, hotelId?: number) =>
  api.get<AlertaPrecio[]>('/api/dashboard/alertas', { params: { umbral, hotelId } }).then((r) => r.data);

export const obtenerGerencial = (anio?: number, mes?: number, hotelId?: number) =>
  api.get<DashboardGerencial>('/api/dashboard/gerencial', { params: { anio, mes, hotelId } }).then((r) => r.data);
