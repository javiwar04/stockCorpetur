import { api } from '../../lib/api';

export type SeveridadAlerta = 'Critica' | 'Alta' | 'Media' | 'Baja';

export interface AlertasResumen {
  total: number;
  criticas: number;
  altas: number;
  medias: number;
  bajas: number;
}

export interface Alerta {
  id: string;
  tipo: string;
  severidad: SeveridadAlerta;
  titulo: string;
  mensaje: string;
  hotelId: number | null;
  hotel: string | null;
  entidad: string | null;
  entidadId: number | null;
  monto: number | null;
  fecha: string | null;
  accionSugerida: string | null;
}

export interface AlertasResultado {
  resumen: AlertasResumen;
  alertas: Alerta[];
}

export const listarAlertas = () =>
  api.get<AlertasResultado>('/api/alertas').then((r) => r.data);

export const obtenerResumenAlertas = () =>
  api.get<AlertasResumen>('/api/alertas/resumen').then((r) => r.data);
