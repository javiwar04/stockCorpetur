export type EstadoDocumentoCompra = 'Borrador' | 'Recibido' | 'Anulado';
export type TipoCompra = 'Ordinaria' | 'Extraordinaria';

export interface DetalleCompra {
  id: number;
  productoId: number;
  productoNombre: string;
  unidadId: number;
  unidadNombre: string;
  hotelId: number;
  hotelNombre: string;
  cantidad: number;
  precioUnitario: number;
  descuento: number;
  total: number;
}

export interface DocumentoCompra {
  id: number;
  fecha: string;
  numeroDocumento: string;
  numeroPedido: string;
  hotelId: number;
  hotelNombre: string;
  proveedorId: number;
  proveedorNombre: string;
  estado: EstadoDocumentoCompra;
  tipoCompra: TipoCompra;
  retencion: number;
  observaciones: string | null;
  total: number;
  detalles: DetalleCompra[];
}

export interface DocumentoCompraResumen {
  id: number;
  fecha: string;
  numeroDocumento: string;
  numeroPedido: string;
  hotelId: number;
  hotelNombre: string;
  proveedorId: number;
  proveedorNombre: string;
  estado: EstadoDocumentoCompra;
  tipoCompra: TipoCompra;
  total: number;
}

export interface LineaNueva {
  hotelId: number | '';
  productoId: number | '';
  unidadId: number | '';
  cantidad: string;
  precioUnitario: string;
  descuento: string;
}

export interface CrearDocumentoCompraRequest {
  fecha: string;
  numeroDocumento: string;
  numeroPedido: string;
  hotelId: number;
  proveedorId: number;
  estado?: EstadoDocumentoCompra;
  tipoCompra?: TipoCompra;
  retencion: number;
  observaciones?: string;
  detalles: { hotelId: number; productoId: number; unidadId: number; cantidad: number; precioUnitario: number; descuento: number }[];
}
