export interface GastoPorHotel {
  hotelId: number;
  hotel: string;
  gasto: number;
  comensales: number | null;
  costoPorComensal: number | null;
  presupuesto: number | null;
  porcentajePresupuesto: number | null;
}

export interface GastoPorCategoria {
  categoria: string;
  gasto: number;
}

export interface ResumenMensual {
  anio: number;
  mes: number;
  gastoTotal: number;
  gastoMesAnterior: number;
  variacionPorcentaje: number;
  documentosRegistrados: number;
  porHotel: GastoPorHotel[];
  porCategoria: GastoPorCategoria[];
}

export interface TopProducto {
  productoId: number;
  producto: string;
  categoria: string;
  unidadBase: string;
  cantidadBase: number;
  gastoTotal: number;
  precioPromedioBase: number;
}

export interface PuntoMensual {
  anio: number;
  mes: number;
  valor: number;
}

export interface TendenciaPrecio {
  productoId: number;
  producto: string;
  unidadBase: string;
  serie: PuntoMensual[];
}

export interface ConsumoHotelSerie {
  hotelId: number;
  hotel: string;
  serie: PuntoMensual[];
}

export interface AlertaPrecio {
  productoId: number;
  producto: string;
  unidadBase: string;
  precioReciente: number;
  precioReferencia: number;
  incrementoPorcentaje: number;
  ultimaCompra: string;
}

export interface TopProveedorSaldo {
  proveedorId: number;
  proveedor: string;
  documentosPendientes: number;
  saldo: number;
  saldoVencido: number;
}

export interface MermaProducto {
  productoId: number;
  producto: string;
  categoria: string;
  unidadBase: string;
  cantidadBase: number;
  valorEstimado: number;
}

export interface StockCritico {
  hotelId: number;
  hotel: string;
  productoId: number;
  producto: string;
  categoria: string;
  unidadBase: string;
  existencia: number;
  stockMinimo: number;
  faltante: number;
  valorFaltanteEstimado: number;
  estadoStock: 'Negativo' | 'SinStock' | 'BajoMinimo';
}

export interface DashboardGerencial {
  anio: number;
  mes: number;
  valorInventarioEstimado: number;
  productosEnRiesgo: number;
  valorFaltanteEstimado: number;
  valorMermasEstimado: number;
  movimientosMerma: number;
  valorAjustesEstimado: number;
  movimientosAjuste: number;
  incluyeFinanzas: boolean;
  saldoCuentasPorPagar: number | null;
  saldoCuentasVencido: number | null;
  documentosVencidos: number | null;
  topProveedoresSaldo: TopProveedorSaldo[];
  topMermas: MermaProducto[];
  stockCritico: StockCritico[];
}
