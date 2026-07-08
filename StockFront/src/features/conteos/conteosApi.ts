import { api } from '../../lib/api';

export interface PlantillaConteoItem {
  productoId: number;
  producto: string;
  categoria: string;
  unidadBase: string;
  existenciaSistemaBase: number;
  stockMinimoBase: number;
  ultimoPrecioBase: number;
  valorSistemaEstimado: number;
}

export interface ConteoInventarioResumen {
  id: number;
  fecha: string;
  hotelId: number;
  hotel: string;
  estado: 'Registrado' | 'Ajustado' | 'Anulado';
  productosContados: number;
  productosConDiferencia: number;
  valorDiferenciaEstimado: number;
  observaciones: string | null;
  creadoEn: string;
  creadoPor: string | null;
  ajustesAplicadosEn: string | null;
  ajustesAplicadosPor: string | null;
}

export interface ConteoInventarioDetalle {
  id: number;
  productoId: number;
  producto: string;
  categoria: string;
  unidadBase: string;
  cantidadSistemaBase: number;
  cantidadFisicaBase: number;
  diferenciaBase: number;
  valorDiferenciaEstimado: number;
  movimientoAjusteId: number | null;
}

export interface ConteoInventario extends ConteoInventarioResumen {
  detalles: ConteoInventarioDetalle[];
}

export interface CrearConteoInventarioRequest {
  fecha: string;
  hotelId: number;
  observaciones?: string;
  detalles: Array<{ productoId: number; cantidadFisicaBase: number }>;
}

export interface FiltroConteos {
  hotelId?: number;
  desde?: string;
  hasta?: string;
}

export const obtenerPlantillaConteo = (hotelId: number, fecha?: string) =>
  api.get<PlantillaConteoItem[]>(`/api/conteos-inventario/plantilla/${hotelId}`, { params: { fecha } }).then((r) => r.data);

export const listarConteos = (params?: FiltroConteos) =>
  api.get<ConteoInventarioResumen[]>('/api/conteos-inventario', { params }).then((r) => r.data);

export const obtenerConteo = (id: number) =>
  api.get<ConteoInventario>(`/api/conteos-inventario/${id}`).then((r) => r.data);

export const crearConteo = (data: CrearConteoInventarioRequest) =>
  api.post<ConteoInventario>('/api/conteos-inventario', data).then((r) => r.data);

export const aplicarAjustesConteo = (id: number) =>
  api.post<ConteoInventario>(`/api/conteos-inventario/${id}/aplicar-ajustes`).then((r) => r.data);

async function descargar(url: string, params: FiltroConteos, nombreArchivo: string) {
  const { data } = await api.get<Blob>(url, { params, responseType: 'blob' });
  const enlace = document.createElement('a');
  enlace.href = URL.createObjectURL(data);
  enlace.download = nombreArchivo;
  enlace.click();
  URL.revokeObjectURL(enlace.href);
}

export const descargarConteosExcel = (params: FiltroConteos) =>
  descargar('/api/reportes/conteos-inventario.xlsx', params, `conteos-inventario-${new Date().toISOString().slice(0, 10)}.xlsx`);

export const descargarConteosPdf = (params: FiltroConteos) =>
  descargar('/api/reportes/conteos-inventario.pdf', params, `conteos-inventario-${new Date().toISOString().slice(0, 10)}.pdf`);
