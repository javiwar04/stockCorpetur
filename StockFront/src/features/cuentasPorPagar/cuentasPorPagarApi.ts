import { api } from '../../lib/api';

export type EstadoCuentaPorPagar = 'Pendiente' | 'Parcial' | 'Pagado' | 'Vencido';

export interface PagoProveedor {
  id: number;
  documentoCompraId: number;
  proveedorId: number;
  proveedorNombre: string;
  fecha: string;
  monto: number;
  metodoPago: string;
  referencia: string | null;
  observaciones: string | null;
  creadoEn: string;
  creadoPor: string | null;
}

export interface CuentaPorPagar {
  documentoCompraId: number;
  fecha: string;
  fechaVencimiento: string;
  numeroDocumento: string;
  hotelId: number;
  hotelNombre: string;
  proveedorId: number;
  proveedorNombre: string;
  diasCredito: number;
  bruto: number;
  retencion: number;
  netoAPagar: number;
  pagado: number;
  saldo: number;
  estado: EstadoCuentaPorPagar;
  pagos: PagoProveedor[];
}

export interface ResumenCuentasPorPagar {
  netoAPagar: number;
  pagado: number;
  saldoPendiente: number;
  saldoVencido: number;
  documentosPendientes: number;
  documentosVencidos: number;
  porVencer: number;
  vencido0A30: number;
  vencido31A60: number;
  vencido61Mas: number;
}

export interface CuentasPorPagarResultado {
  resumen: ResumenCuentasPorPagar;
  cuentas: CuentaPorPagar[];
}

export interface FiltroCuentasPorPagar {
  hotelId?: number;
  proveedorId?: number;
  desde?: string;
  hasta?: string;
  soloPendientes?: boolean;
}

export interface RegistrarPagoProveedorRequest {
  documentoCompraId: number;
  fecha: string;
  monto: number;
  metodoPago: string;
  referencia?: string;
  observaciones?: string;
}

export const listarCuentasPorPagar = (params: FiltroCuentasPorPagar) =>
  api.get<CuentasPorPagarResultado>('/api/cuentas-por-pagar', { params }).then((r) => r.data);

export const registrarPagoProveedor = (data: RegistrarPagoProveedorRequest) =>
  api.post<PagoProveedor>('/api/cuentas-por-pagar/pagos', data).then((r) => r.data);

export const eliminarPagoProveedor = (id: number) =>
  api.delete(`/api/cuentas-por-pagar/pagos/${id}`);

export async function descargarCuentasPorPagarExcel(params: FiltroCuentasPorPagar) {
  const { data } = await api.get<Blob>('/api/reportes/cuentas-por-pagar.xlsx', {
    params,
    responseType: 'blob',
  });
  const enlace = document.createElement('a');
  enlace.href = URL.createObjectURL(data);
  enlace.download = `cuentas-por-pagar-${new Date().toISOString().slice(0, 10)}.xlsx`;
  enlace.click();
  URL.revokeObjectURL(enlace.href);
}
