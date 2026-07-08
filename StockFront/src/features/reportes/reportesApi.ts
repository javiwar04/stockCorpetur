import { api } from '../../lib/api';

export interface FiltroReporte {
  hotelId?: number;
  proveedorId?: number;
  desde?: string;
  hasta?: string;
}

export interface ResultadoImportacion {
  hojasProcesadas: number;
  documentosCreados: number;
  documentosOmitidos: number;
  productosCreados: number;
  lineasCreadas: number;
  hojasNoReconocidas: string[];
  advertencias: string[];
}

/** Descarga un reporte y dispara el guardado en el navegador. */
async function descargar(url: string, params: FiltroReporte, nombreArchivo: string) {
  const { data } = await api.get<Blob>(url, { params, responseType: 'blob' });
  const enlace = document.createElement('a');
  enlace.href = URL.createObjectURL(data);
  enlace.download = nombreArchivo;
  enlace.click();
  URL.revokeObjectURL(enlace.href);
}

export const descargarExcel = (filtro: FiltroReporte) =>
  descargar('/api/reportes/compras.xlsx', filtro, `reporte-compras-${new Date().toISOString().slice(0, 10)}.xlsx`);

export const descargarPdf = (filtro: FiltroReporte) =>
  descargar('/api/reportes/compras.pdf', filtro, `reporte-compras-${new Date().toISOString().slice(0, 10)}.pdf`);

export const importarExcel = (archivo: File) => {
  const form = new FormData();
  form.append('archivo', archivo);
  return api.post<ResultadoImportacion>('/api/importacion/excel', form).then((r) => r.data);
};
