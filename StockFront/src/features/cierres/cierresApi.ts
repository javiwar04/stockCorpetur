import { api } from '../../lib/api';

export interface CierreMensual {
  id: number;
  hotelId: number;
  hotel: string;
  anio: number;
  mes: number;
  estado: 'Preliminar' | 'Cerrado' | 'Anulado';
  comprasTotal: number;
  documentosCompra: number;
  valorInventarioEstimado: number;
  productosEnRiesgo: number;
  valorFaltanteEstimado: number;
  valorMermasEstimado: number;
  movimientosMerma: number;
  valorAjustesEstimado: number;
  movimientosAjuste: number;
  conteosFisicos: number;
  valorDiferenciasConteo: number;
  saldoCuentasPorPagar: number;
  saldoCuentasVencido: number;
  documentosVencidos: number;
  fechaCierre: string;
  observaciones: string | null;
  creadoEn: string;
  creadoPor: string | null;
}

export interface FiltroCierresMensuales {
  hotelId?: number;
  anio?: number;
  mes?: number;
}

export interface CerrarMesRequest {
  hotelId: number;
  anio: number;
  mes: number;
  observaciones?: string;
}

export interface AnularCierreMensualRequest {
  motivo?: string;
}

export const listarCierresMensuales = (params?: FiltroCierresMensuales) =>
  api.get<CierreMensual[]>('/api/cierres-mensuales', { params }).then((r) => r.data);

export const previewCierreMensual = (hotelId: number, anio: number, mes: number) =>
  api.get<CierreMensual>('/api/cierres-mensuales/preview', { params: { hotelId, anio, mes } }).then((r) => r.data);

export const cerrarMes = (data: CerrarMesRequest) =>
  api.post<CierreMensual>('/api/cierres-mensuales', data).then((r) => r.data);

export const anularCierreMensual = (id: number, data: AnularCierreMensualRequest) =>
  api.post<CierreMensual>(`/api/cierres-mensuales/${id}/anular`, data).then((r) => r.data);

async function descargar(url: string, params: FiltroCierresMensuales, nombreArchivo: string) {
  const { data } = await api.get<Blob>(url, { params, responseType: 'blob' });
  const enlace = document.createElement('a');
  enlace.href = URL.createObjectURL(data);
  enlace.download = nombreArchivo;
  enlace.click();
  URL.revokeObjectURL(enlace.href);
}

export const descargarCierresExcel = (params: FiltroCierresMensuales) =>
  descargar('/api/reportes/cierres-mensuales.xlsx', params, `cierres-mensuales-${new Date().toISOString().slice(0, 10)}.xlsx`);

export const descargarCierresPdf = (params: FiltroCierresMensuales) =>
  descargar('/api/reportes/cierres-mensuales.pdf', params, `cierres-mensuales-${new Date().toISOString().slice(0, 10)}.pdf`);
