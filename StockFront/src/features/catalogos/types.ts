export const CATEGORIAS = ['Verdura', 'Fruta', 'Condimento', 'Lacteo', 'Proteina', 'Otros'] as const;
export type Categoria = (typeof CATEGORIAS)[number];

export interface Producto {
  id: number;
  nombre: string;
  categoria: Categoria;
  activo: boolean;
  unidadBaseId: number;
  unidadBaseNombre: string;
}

export interface Proveedor {
  id: number;
  nombre: string;
  nit: string | null;
  telefono: string | null;
  diasCredito: number;
  activo: boolean;
}

export interface Unidad {
  id: number;
  nombre: string;
  abreviatura: string;
}

export interface Hotel {
  id: number;
  nombre: string;
  activo: boolean;
}

export interface Conversion {
  id: number;
  unidadId: number;
  unidadNombre: string;
  factorABase: number;
}
